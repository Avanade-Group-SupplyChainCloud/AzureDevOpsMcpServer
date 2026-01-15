using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
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
}
