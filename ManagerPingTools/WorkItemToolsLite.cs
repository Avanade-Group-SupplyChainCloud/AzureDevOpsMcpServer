using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Helpers;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.ManagerPingTools;

[McpServerToolType]
public class WorkItemToolsLite(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "get_work_item")]
    [Description("Get a work item by ID (fields/relations).")]
    public async Task<WorkItem> GetWorkItem(
        [Description("The ID of the work item.")] int id,
        [Description("The expand level for the work item.")] WorkItemExpand expand = WorkItemExpand.None
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetWorkItemAsync(project, id, null, null, expand);
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
        return JsonSerializer.Serialize(comments, new JsonSerializerOptions { WriteIndented = true });
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

    [McpServerTool(Name = "create_work_item")]
    [Description("Create a new work item (Bug, Task, User Story, etc.).")]
    public async Task<WorkItem> CreateWorkItem(
        [Description("The type of work item to create (e.g., 'Bug', 'Task').")]
            string workItemType,
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

            var allChildren = await GetAllChildrenRecursiveAsync(client, project, parentWorkItem);
            var summary = BuildExecutiveSummary(parentWorkItem, allChildren);

            return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
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

        var childRelations = workItem
            .Relations.Where(r =>
                r.Rel == "System.LinkTypes.Hierarchy-Forward" && r.Url.Contains("workItems/")
            )
            .ToList();

        if (!childRelations.Any())
            return allChildren;

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

        var children = await client.GetWorkItemsAsync(
            project,
            childIds,
            expand: WorkItemExpand.Relations
        );

        foreach (var child in children)
        {
            allChildren.Add(child);
            var grandChildren = await GetAllChildrenRecursiveAsync(client, project, child);
            allChildren.AddRange(grandChildren);
        }

        return allChildren;
    }

    private object BuildExecutiveSummary(WorkItem parent, List<WorkItem> allChildren)
    {
        string GetFieldValue(WorkItem wi, string fieldName) =>
            wi.Fields != null && wi.Fields.TryGetValue(fieldName, out var value)
                ? value?.ToString()
                : "";

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

        var childrenByType = allChildren
            .GroupBy(c => GetFieldValue(c, "System.WorkItemType"))
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                Items = g.ToList(),
            })
            .ToList();

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

        var completedStates = new[] { "Done", "Closed", "Completed", "Resolved" };
        var completedCount = allChildren.Count(c =>
            completedStates.Contains(GetFieldValue(c, "System.State"))
        );
        var totalCount = allChildren.Count;
        var completionPercentage =
            totalCount > 0 ? Math.Round((double)completedCount / totalCount * 100, 1) : 0;

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
