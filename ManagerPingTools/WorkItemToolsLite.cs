using System.ComponentModel;
using System.Text.Json;
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
}
