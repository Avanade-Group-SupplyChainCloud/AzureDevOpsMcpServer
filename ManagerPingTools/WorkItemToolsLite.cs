using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
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
}
