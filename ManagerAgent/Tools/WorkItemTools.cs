using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class WorkItemTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

        [McpServerTool(Name = "get_work_item")]
        [Description("Get a work item by ID with optional field expansion.")]
        public async Task<WorkItem> GetWorkItem(
            [Description("The ID of the work item.")] int id,
            [Description("The project name or ID.")] string project,
            [Description("A list of fields to include in the response.")]
                IEnumerable<string> fields = null,
            [Description("The date and time to retrieve the work item as of.")]
                DateTime? asOf = null,
            [Description("The expand level for the work item.")]
                WorkItemExpand expand = WorkItemExpand.None
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            return await client.GetWorkItemAsync(project, id, fields, asOf, expand);
        }

        [McpServerTool(Name = "get_work_items_batch")]
        [Description("Get multiple work items by IDs in a single batch request.")]
        public async Task<IEnumerable<WorkItem>> GetWorkItemsBatch(
            [Description("The project name or ID.")] string project,
            [Description("List of work item IDs to retrieve.")] IEnumerable<int> ids,
            [Description("A list of fields to include in the response.")]
                IEnumerable<string> fields = null
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var workItems = await client.GetWorkItemsAsync(project, ids, fields);
            return workItems ?? Enumerable.Empty<WorkItem>();
        }

        [McpServerTool(Name = "create_work_item")]
        [Description("Create a new work item (Bug, Task, User Story, etc.) in a project.")]
        public async Task<WorkItem> CreateWorkItem(
            [Description("The project name or ID.")] string project,
            [Description("The type of work item to create (e.g., 'Bug', 'Task').")]
                string workItemType,
            [Description("A dictionary of fields and their values for the new work item.")]
                Dictionary<string, object> fields
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var patchDocument = new JsonPatchDocument();

            foreach (var field in fields)
            {
                patchDocument.Add(
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value,
                    }
                );
            }

            return await client.CreateWorkItemAsync(patchDocument, project, workItemType);
        }

        [McpServerTool(Name = "update_work_item")]
        [Description("Update fields on an existing work item.")]
        public async Task<WorkItem> UpdateWorkItem(
            [Description("The ID of the work item to update.")] int id,
            [Description("The project name or ID.")] string project,
            [Description("A dictionary of fields and their values to update.")]
                Dictionary<string, object> updates
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var patchDocument = new JsonPatchDocument();

            foreach (var update in updates)
            {
                patchDocument.Add(
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{update.Key}",
                        Value = update.Value,
                    }
                );
            }

            return await client.UpdateWorkItemAsync(patchDocument, id);
        }

        [McpServerTool(Name = "my_work_items")]
        [Description("Get work items assigned to or created by the current user.")]
        public async Task<string> MyWorkItems(
            [Description("The project name or ID.")] string project,
            [Description("Filter by work item type.")] string type = null,
            [Description("Include completed work items.")] bool includeCompleted = false,
            [Description("Maximum number of results.")] int top = 50
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var wiql =
                $"SELECT [System.Id], [System.Title], [System.State], [System.AssignedTo] FROM WorkItems WHERE [System.TeamProject] = '{project}' AND ([System.AssignedTo] = @Me OR [System.CreatedBy] = @Me)";

            if (!includeCompleted)
                wiql +=
                    " AND [System.State] <> 'Closed' AND [System.State] <> 'Done' AND [System.State] <> 'Removed'";

            if (!string.IsNullOrEmpty(type))
                wiql += $" AND [System.WorkItemType] = '{type}'";

            wiql += $" ORDER BY [System.ChangedDate] DESC";

            var query = new Wiql { Query = wiql };
            var result = await client.QueryByWiqlAsync(query, project, top: top);

            if (result?.WorkItems == null || !result.WorkItems.Any())
                return "[]";

            var ids = result.WorkItems.Select(w => w.Id).ToList();
            var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
            return JsonSerializer.Serialize(
                workItems,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        [McpServerTool(Name = "list_work_item_comments")]
        [Description("Get comments on a work item.")]
        public async Task<string> ListWorkItemComments(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the work item.")] int workItemId,
            [Description("Maximum number of comments to return.")] int top = 100
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var comments = await client.GetCommentsAsync(project, workItemId, top);
            return JsonSerializer.Serialize(
                comments,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        [McpServerTool(Name = "add_work_item_comment")]
        [Description("Add a comment to a work item.")]
        public async Task<string> AddWorkItemComment(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the work item.")] int workItemId,
            [Description("The comment text.")] string comment
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var request = new CommentCreate { Text = comment };
            var result = await client.AddCommentAsync(request, project, workItemId);
            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        [McpServerTool(Name = "list_work_item_revisions")]
        [Description("Get revision history of a work item.")]
        public async Task<IEnumerable<WorkItem>> ListWorkItemRevisions(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the work item.")] int workItemId,
            [Description("Maximum number of revisions.")] int top = 100,
            [Description("Number of revisions to skip.")] int skip = 0
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var revisions = await client.GetRevisionsAsync(project, workItemId, top, skip);
            return revisions ?? Enumerable.Empty<WorkItem>();
        }

        [McpServerTool(Name = "get_work_item_type")]
        [Description("Get details of a work item type including its fields and rules.")]
        public async Task<WorkItemType> GetWorkItemType(
            [Description("The project name or ID.")] string project,
            [Description("The work item type name.")] string workItemType
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            return await client.GetWorkItemTypeAsync(project, workItemType);
        }

        [McpServerTool(Name = "add_child_work_items")]
        [Description("Create child work items under a parent work item.")]
        public async Task<IEnumerable<WorkItem>> AddChildWorkItems(
            [Description("The ID of the parent work item.")] int parentId,
            [Description("The project name or ID.")] string project,
            [Description("The type of child work items to create.")] string workItemType,
            [Description("List of child work item titles.")] IEnumerable<string> titles
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var results = new List<WorkItem>();

            foreach (var title in titles)
            {
                var patchDocument = new JsonPatchDocument
                {
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = "/fields/System.Title",
                        Value = title,
                    },
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = "/relations/-",
                        Value = new
                        {
                            rel = "System.LinkTypes.Hierarchy-Reverse",
                            url = $"{_adoService.Connection.Uri}_apis/wit/workItems/{parentId}",
                        },
                    },
                };

                var workItem = await client.CreateWorkItemAsync(
                    patchDocument,
                    project,
                    workItemType
                );
                results.Add(workItem);
            }

            return results;
        }

        [McpServerTool(Name = "update_work_items_batch")]
        [Description("Update multiple work items in batch.")]
        public async Task<string> UpdateWorkItemsBatch(
            [Description(
                "JSON array of updates: [{id, op, path, value, format}]. op can be 'Add', 'Replace', or 'Remove'. format is optional ('Html' or 'Markdown')."
            )]
                string updatesJson
        )
        {
            var connection = _adoService.Connection;
            var baseUrl = connection.Uri.ToString().TrimEnd('/');
            var url = $"{baseUrl}/_apis/wit/$batch?api-version=7.1";

            // Parse the updates JSON and group by work item ID
            var updates =
                JsonSerializer.Deserialize<List<BatchUpdateInput>>(updatesJson)
                ?? new List<BatchUpdateInput>();
            var uniqueIds = updates.Select(u => u.Id).Distinct().ToList();

            var body = uniqueIds
                .Select(id => new
                {
                    method = "PATCH",
                    uri = $"/_apis/wit/workitems/{id}?api-version=7.1",
                    headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json-patch+json" },
                    },
                    body = updates
                        .Where(u => u.Id == id)
                        .Select(u => new
                        {
                            op = u.Op?.ToLower() ?? "add",
                            path = u.Path,
                            value = u.Value,
                        })
                        .ToArray(),
                })
                .ToArray();

            using var httpClient = _adoService.CreateHttpClient();
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json"
                ),
            };
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Error updating work items in batch: {response.StatusCode} - {content}";

            return content;
        }

        private class BatchUpdateInput
        {
            public int Id { get; set; }
            public string Op { get; set; }
            public string Path { get; set; } = "";
            public string Value { get; set; } = "";
            public string Format { get; set; }
        }

        [McpServerTool(Name = "link_work_items")]
        [Description("Link two work items together with a specified link type.")]
        public async Task<WorkItem> LinkWorkItems(
            [Description("The ID of the source work item.")] int sourceId,
            [Description("The ID of the target work item.")] int targetId,
            [Description("The project name or ID.")] string project,
            [Description("Link type: 'parent', 'child', 'related', 'predecessor', 'successor'.")]
                string linkType = "related"
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();

            var linkTypeName = linkType.ToLower() switch
            {
                "parent" => "System.LinkTypes.Hierarchy-Reverse",
                "child" => "System.LinkTypes.Hierarchy-Forward",
                "predecessor" => "System.LinkTypes.Dependency-Reverse",
                "successor" => "System.LinkTypes.Dependency-Forward",
                _ => "System.LinkTypes.Related",
            };

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = linkTypeName,
                        url = $"{_adoService.Connection.Uri}_apis/wit/workItems/{targetId}",
                    },
                },
            };

            return await client.UpdateWorkItemAsync(patchDocument, sourceId);
        }

        [McpServerTool(Name = "unlink_work_item")]
        [Description("Remove a link from a work item.")]
        public async Task<WorkItem> UnlinkWorkItem(
            [Description("The ID of the work item.")] int id,
            [Description("The project name or ID.")] string project,
            [Description("The index of the relation to remove.")] int relationIndex
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Remove,
                    Path = $"/relations/{relationIndex}",
                },
            };

            return await client.UpdateWorkItemAsync(patchDocument, id);
        }

        [McpServerTool(Name = "link_work_item_to_pull_request")]
        [Description("Link a work item to a pull request.")]
        public async Task<WorkItem> LinkWorkItemToPullRequest(
            [Description("The ID of the work item.")] int workItemId,
            [Description("The project ID (GUID).")] string projectId,
            [Description("The repository ID.")] string repositoryId,
            [Description("The pull request ID.")] int pullRequestId
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();

            var artifactUri =
                $"vstfs:///Git/PullRequestId/{projectId}%2F{repositoryId}%2F{pullRequestId}";

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "ArtifactLink",
                        url = artifactUri,
                        attributes = new { name = "Pull Request" },
                    },
                },
            };

            return await client.UpdateWorkItemAsync(patchDocument, workItemId);
        }

        [McpServerTool(Name = "add_artifact_link")]
        [Description("Add an artifact link (commit, build, branch) to a work item.")]
        public async Task<WorkItem> AddArtifactLink(
            [Description("The ID of the work item.")] int workItemId,
            [Description("The project name or ID.")] string project,
            [Description("The artifact URI.")] string artifactUri,
            [Description("The link type name (e.g., 'Build', 'Branch', 'Fixed in Commit').")]
                string linkType = "ArtifactLink",
            [Description("A comment for the link.")] string comment = null
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "ArtifactLink",
                        url = artifactUri,
                        attributes = new { name = linkType, comment = comment ?? "" },
                    },
                },
            };

            return await client.UpdateWorkItemAsync(patchDocument, workItemId);
        }

        [McpServerTool(Name = "list_backlogs")]
        [Description("List backlogs for a team in a project.")]
        public async Task<string> ListBacklogs(
            [Description("The project name or ID.")] string project,
            [Description("The team name or ID.")] string team
        )
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new Microsoft.TeamFoundation.Core.WebApi.Types.TeamContext(
                project,
                team
            );
            var backlogs = await client.GetBacklogsAsync(teamContext);
            return JsonSerializer.Serialize(
                backlogs,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        [McpServerTool(Name = "list_backlog_work_items")]
        [Description("Get work items in a specific backlog.")]
        public async Task<string> ListBacklogWorkItems(
            [Description("The project name or ID.")] string project,
            [Description("The team name or ID.")] string team,
            [Description("The backlog ID.")] string backlogId
        )
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new Microsoft.TeamFoundation.Core.WebApi.Types.TeamContext(
                project,
                team
            );
            // Using REST API as SDK method signature differs
            var connection = _adoService.Connection;
            var baseUrl = connection.Uri.ToString().TrimEnd('/');
            var url =
                $"{baseUrl}/{project}/{team}/_apis/work/backlogs/{backlogId}/workItems?api-version=7.1";

            using var httpClient = _adoService.CreateHttpClient();
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"Error getting backlog work items: {response.StatusCode} - {content}";

            return content;
        }

        [McpServerTool(Name = "get_query")]
        [Description("Get a saved work item query by ID or path.")]
        public async Task<QueryHierarchyItem> GetQuery(
            [Description("The project name or ID.")] string project,
            [Description("The query ID or path.")] string query,
            [Description("Expand level for children.")] int depth = 0
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            return await client.GetQueryAsync(project, query, depth: depth);
        }

        [McpServerTool(Name = "get_query_results")]
        [Description("Execute a saved query and get results.")]
        public async Task<string> GetQueryResults(
            [Description("The query ID.")] string queryId,
            [Description("The project name or ID.")] string project = null,
            [Description("Maximum number of results.")] int top = 100
        )
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var result = await client.QueryByIdAsync(project, Guid.Parse(queryId));

            if (result?.WorkItems == null || !result.WorkItems.Any())
                return "[]";

            var ids = result.WorkItems.Take(top).Select(w => w.Id).ToList();
            var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
            return JsonSerializer.Serialize(
                workItems,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
    }