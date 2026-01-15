using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.ManagerPingTools;

[McpServerToolType]
public class QueryToolsLite(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "run_wiql_query")]
    [Description("Execute a WIQL (Work Item Query Language) query and return matching work items.")]
    public async Task<IEnumerable<WorkItem>> RunWiqlQuery(
        [Description(
            "The WIQL query string. Example: \"SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'\""
        )]
            string wiqlQuery,
        [Description("Maximum number of results to return. Defaults to 200.")] int top = 200
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;

        var wiql = new Wiql { Query = wiqlQuery };
        var result = await client.QueryByWiqlAsync(wiql, project, top: top);

        if (result.WorkItems == null || !result.WorkItems.Any())
            return Enumerable.Empty<WorkItem>();

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
        return workItems ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "get_work_items_by_ids")]
    [Description("Get multiple work items by their IDs in a single request.")]
    public async Task<IEnumerable<WorkItem>> GetWorkItemsByIds(
        [Description("Array of work item IDs to retrieve.")] int[] ids,
        [Description("Expand level: 'None', 'Relations', 'Fields', 'Links', 'All'.")]
            string expand = "Fields"
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;

        WorkItemExpand expandEnum = expand.ToLower() switch
        {
            "relations" => WorkItemExpand.Relations,
            "links" => WorkItemExpand.Links,
            "all" => WorkItemExpand.All,
            "none" => WorkItemExpand.None,
            _ => WorkItemExpand.Fields,
        };

        var workItems = await client.GetWorkItemsAsync(project, ids, expand: expandEnum);
        return workItems ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "get_work_item_types")]
    [Description("Get all work item types available.")]
    public async Task<IEnumerable<WorkItemType>> GetWorkItemTypes()
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var types = await client.GetWorkItemTypesAsync(project);
        return types ?? Enumerable.Empty<WorkItemType>();
    }

    [McpServerTool(Name = "get_work_item_fields")]
    [Description("Get all fields available for work items.")]
    public async Task<IEnumerable<WorkItemField>> GetWorkItemFields()
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var fields = await client.GetFieldsAsync();
        return fields ?? Enumerable.Empty<WorkItemField>();
    }

    [McpServerTool(Name = "get_work_item_history")]
    [Description("Get the revision history of a work item.")]
    public async Task<IEnumerable<WorkItem>> GetWorkItemHistory(
        [Description("The ID of the work item.")] int id,
        [Description("Maximum number of revisions to return. Defaults to 50.")] int top = 50
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var revisions = await client.GetRevisionsAsync(project, id, top: top);
        return revisions ?? Enumerable.Empty<WorkItem>();
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
        [Description("The query ID (GUID).")]
            string queryId,
        [Description("Maximum number of results.")]
            int top = 100
    )
    {
        if (!Guid.TryParse(queryId, out var queryGuid))
            throw new ArgumentException("queryId must be a GUID.");

        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;
        var result = await client.QueryByIdAsync(project, queryGuid);

        if (result?.WorkItems == null || !result.WorkItems.Any())
            return "[]";

        var ids = result.WorkItems.Take(top).Select(w => w.Id).ToList();
        var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
        return JsonSerializer.Serialize(workItems, new JsonSerializerOptions { WriteIndented = true });
    }
}
