using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Helpers;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
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
    [Description(
        "Get the raw work item by ID (fields/relations). Use this for data retrieval; if the user asks for a summary/update/status, prefer get_executive_summary instead."
    )]
    public async Task<WorkItem> GetWorkItem(
        [Description("The ID of the work item.")] int id,
        [Description("A list of fields to include in the response.")]
            IEnumerable<string> fields = null,
        [Description("The date and time to retrieve the work item as of.")] DateTime? asOf = null,
        [Description("The expand level for the work item.")]
            WorkItemExpand expand = WorkItemExpand.None
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetWorkItemAsync(project, id, fields, asOf, expand);
    }

    //[McpServerTool(Name = "get_work_items_batch")]
    //[Description("Get multiple work items by IDs in a single batch request.")]
    //public async Task<IEnumerable<WorkItem>> GetWorkItemsBatch(
    //    [Description("List of work item IDs to retrieve.")] IEnumerable<int> ids,
    //    [Description("A list of fields to include in the response.")]
    //        IEnumerable<string> fields = null
    //)
    //{
    //    var client = await _adoService.GetWorkItemTrackingApiAsync();
    //    var project = _adoService.DefaultProject;
    //    var workItems = await client.GetWorkItemsAsync(project, ids, fields);
    //    return workItems ?? Enumerable.Empty<WorkItem>();
    //}

    [McpServerTool(Name = "create_work_item")]
    [Description("Create a new work item (Bug, Task, User Story, etc.).")]
    public async Task<WorkItem> CreateWorkItem(
        [Description("The type of work item to create (e.g., 'Bug', 'Task').")] string workItemType,
        [Description("A dictionary of fields and their values for the new work item.")]
            Dictionary<string, object> fields
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
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
    public async Task<string> UpdateWorkItem(
        [Description("The ID of the work item to update.")] int id,
        [Description(
            "JSON array of field updates. Example: [{\"field\":\"System.Title\",\"value\":\"New Title\"},{\"field\":\"Microsoft.VSTS.Scheduling.OriginalEstimate\",\"value\":8}]"
        )]
            string updatesJson
    )
    {
        return await ErrorHandler.ExecuteWithErrorHandling(async () =>
        {
            if (string.IsNullOrWhiteSpace(updatesJson))
                throw new ArgumentException(
                    "Parameter 'updates' is required and must contain at least one field update."
                );

            // Parse the JSON string into a list of updates
            var parsedUpdates =
                JsonSerializer.Deserialize<List<FieldUpdateDto>>(
                    updatesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? throw new ArgumentException("Could not parse 'updates' as a JSON array.");

            if (parsedUpdates.Count == 0)
                throw new ArgumentException("'updates' must contain at least one field update.");

            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var patchDocument = new JsonPatchDocument();

            foreach (var update in parsedUpdates)
            {
                // Handle different value types - could be JsonElement from deserialization
                object value = update.Value switch
                {
                    JsonElement je => je.ValueKind switch
                    {
                        JsonValueKind.String => je.GetString(),
                        JsonValueKind.Number => je.TryGetInt64(out var l)
                            ? (object)l
                            : je.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => je.GetRawText(),
                    },
                    _ => update.Value, // Already a primitive (string, int, bool, etc.)
                };

                patchDocument.Add(
                    new JsonPatchOperation
                    {
                        Operation = Operation.Replace,
                        Path = $"/fields/{update.Field}",
                        Value = value,
                    }
                );
            }

            var result = await client.UpdateWorkItemAsync(patchDocument, id);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    id = result.Id,
                    rev = result.Rev,
                }
            );
        });
    }

    private class FieldUpdateDto
    {
        public string Field { get; set; } = "";
        public object Value { get; set; }
    }

    [McpServerTool(Name = "list_work_item_comments")]
    [Description("Get comments on a work item.")]
    public async Task<string> ListWorkItemComments(
        [Description("The ID of the work item.")] int workItemId,
        [Description("Maximum number of comments to return.")] int top = 100
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var comments = await client.GetCommentsAsync(project, workItemId, top);
        return JsonSerializer.Serialize(
            comments,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    [McpServerTool(Name = "add_work_item_comment")]
    [Description("Add a comment to a work item.")]
    public async Task<string> AddWorkItemComment(
        [Description("The ID of the work item.")] int workItemId,
        [Description("The comment text.")] string comment
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var request = new CommentCreate { Text = comment };
        var result = await client.AddCommentAsync(request, project, workItemId);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "list_work_item_revisions")]
    [Description("Get revision history of a work item.")]
    public async Task<IEnumerable<WorkItem>> ListWorkItemRevisions(
        [Description("The ID of the work item.")] int workItemId,
        [Description("Maximum number of revisions.")] int top = 100,
        [Description("Number of revisions to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var revisions = await client.GetRevisionsAsync(project, workItemId, top, skip);
        return revisions ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "add_child_work_items")]
    [Description("Create child work items under a parent work item.")]
    public async Task<IEnumerable<WorkItem>> AddChildWorkItems(
        [Description("The ID of the parent work item.")] int parentId,
        [Description("The type of child work items to create.")] string workItemType,
        [Description("List of child work item titles.")] IEnumerable<string> titles
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
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

            var workItem = await client.CreateWorkItemAsync(patchDocument, project, workItemType);
            results.Add(workItem);
        }

        return results;
    }

    [McpServerTool(Name = "link_work_items")]
    [Description("Link two work items together with a specified link type.")]
    public async Task<WorkItem> LinkWorkItems(
        [Description("The ID of the source work item.")] int sourceId,
        [Description("The ID of the target work item.")] int targetId,
        [Description("Link type: 'parent', 'child', 'related', 'predecessor', 'successor'.")]
            string linkType = "related",
        [Description("Optional comment to include on the link.")] string comment = ""
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

        var orgUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var targetUrl = $"{orgUrl}/_apis/wit/workItems/{targetId}";

        object attributes = string.IsNullOrWhiteSpace(comment) ? null : new { comment = comment };

        var patchDocument = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/relations/-",
                Value = new
                {
                    rel = linkTypeName,
                    url = targetUrl,
                    attributes = attributes,
                },
            },
        };

        return await client.UpdateWorkItemAsync(patchDocument, sourceId);
    }

    [McpServerTool(Name = "get_query")]
    [Description("Get a saved work item query by ID or path.")]
    public async Task<QueryHierarchyItem> GetQuery(
        [Description("The query ID or path.")] string query,
        [Description("Expand level for children.")] int depth = 0
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetQueryAsync(project, query, depth: depth);
    }

    [McpServerTool(Name = "get_query_results")]
    [Description("Execute a saved query and get results.")]
    public async Task<string> GetQueryResults(
        [Description("The query ID.")] string queryId,
        [Description("Maximum number of results.")] int top = 100
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
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

    [McpServerTool(Name = "get_executive_summary")]
    [Description(
        "Get an executive summary (summary/update/status) for a work item, including all children recursively (roadmap, status, progress)."
    )]
    public async Task<string> GetExecutiveSummary(
        [Description("The ID of the work item to get updates/details for.")] int workItemId
    )
    {
        return await ErrorHandler.ExecuteWithErrorHandling(async () =>
        {
            var client = await _adoService.GetWorkItemTrackingApiAsync();
            var project = _adoService.DefaultProject;

            // Get the parent work item with relations
            var parentWorkItem = await client.GetWorkItemAsync(
                project,
                workItemId,
                expand: WorkItemExpand.All
            );

            if (parentWorkItem == null)
                return JsonSerializer.Serialize(
                    new { error = $"Work item {workItemId} not found." },
                    new JsonSerializerOptions { WriteIndented = true }
                );

            // Recursively get all children
            var allChildren = await GetAllChildrenRecursiveAsync(client, project, parentWorkItem);

            // Build executive summary
            var summary = BuildExecutiveSummary(parentWorkItem, allChildren);

            return JsonSerializer.Serialize(
                summary,
                new JsonSerializerOptions { WriteIndented = true }
            );
        });
    }

    private async Task<List<WorkItem>> GetAllChildrenRecursiveAsync(
        WorkItemTrackingHttpClient client,
        string project,
        WorkItem workItem
    )
    {
        var allChildren = new List<WorkItem>();

        if (workItem.Relations == null)
            return allChildren;

        // Get direct children
        var childRelations = workItem
            .Relations.Where(r =>
                r.Rel == "System.LinkTypes.Hierarchy-Forward" && r.Url.Contains("workItems/")
            )
            .ToList();

        if (!childRelations.Any())
            return allChildren;

        // Extract child IDs
        var childIds = childRelations
            .Select(r =>
            {
                var url = r.Url;
                var lastSlash = url.LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(url.AsSpan(lastSlash + 1), out var id))
                    return id;
                return -1;
            })
            .Where(id => id > 0)
            .ToList();

        if (!childIds.Any())
            return allChildren;

        // Get all child work items
        var children = await client.GetWorkItemsAsync(
            project,
            childIds,
            expand: WorkItemExpand.Relations
        );

        foreach (var child in children)
        {
            allChildren.Add(child);
            // Recursively get children of children
            var grandChildren = await GetAllChildrenRecursiveAsync(client, project, child);
            allChildren.AddRange(grandChildren);
        }

        return allChildren;
    }

    private object BuildExecutiveSummary(WorkItem parent, List<WorkItem> allChildren)
    {
        // Helper to safely get field value
        string GetFieldValue(WorkItem wi, string fieldName) =>
            wi.Fields != null && wi.Fields.TryGetValue(fieldName, out var value)
                ? value?.ToString()
                : "";

        // Parent info
        var parentInfo = new
        {
            parent.Id,
            Title = GetFieldValue(parent, "System.Title"),
            Type = GetFieldValue(parent, "System.WorkItemType"),
            State = GetFieldValue(parent, "System.State"),
            AssignedTo = GetFieldValue(parent, "System.AssignedTo"),
            IterationPath = GetFieldValue(parent, "System.IterationPath"),
            AreaPath = GetFieldValue(parent, "System.AreaPath"),
            Priority = GetFieldValue(parent, "Microsoft.VSTS.Common.Priority"),
            Description = GetFieldValue(parent, "System.Description"),
        };

        // Group children by type
        var childrenByType = allChildren
            .GroupBy(c => GetFieldValue(c, "System.WorkItemType"))
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                Items = g.ToList(),
            })
            .ToList();

        // Group children by state
        var childrenByState = allChildren
            .GroupBy(c => GetFieldValue(c, "System.State"))
            .Select(g => new
            {
                State = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((double)g.Count() / allChildren.Count * 100, 1),
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        // Calculate completion metrics
        var completedStates = new[] { "Done", "Closed", "Completed", "Resolved" };
        var completedCount = allChildren.Count(c =>
            completedStates.Contains(GetFieldValue(c, "System.State"))
        );
        var totalCount = allChildren.Count;
        var completionPercentage =
            totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;

        // Group by assigned to
        var teamWorkload = allChildren
            .GroupBy(c => GetFieldValue(c, "System.AssignedTo"))
            .Select(g => new
            {
                AssignedTo = string.IsNullOrWhiteSpace(g.Key) ? "Unassigned" : g.Key,
                Count = g.Count(),
            })
            .OrderByDescending(g => g.Count)
            .ToList();

        return new
        {
            Parent = parentInfo,
            Summary = new
            {
                TotalChildren = totalCount,
                Completed = completedCount,
                CompletionPercentage = completionPercentage,
                InProgress = allChildren.Count(c => GetFieldValue(c, "System.State") == "Active"),
                NotStarted = allChildren.Count(c => GetFieldValue(c, "System.State") == "New"),
            },
            ChildrenByType = childrenByType,
            ChildrenByState = childrenByState,
            TeamWorkload = teamWorkload,
        };
    }
}