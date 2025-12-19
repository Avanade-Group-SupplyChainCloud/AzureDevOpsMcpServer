using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class QueryTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "run_wiql_query")]
    [Description("Execute a WIQL (Work Item Query Language) query and return matching work items.")]
    public async Task<IEnumerable<WorkItem>> RunWiqlQuery(
        [Description(
            "The WIQL query string. Example: \"SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'\""
        )]
            string wiqlQuery,
        [Description("The project name or ID (required for project-scoped queries).")]
            string project,
        [Description("Maximum number of results to return.")] int top = 200
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();

        var wiql = new Wiql { Query = wiqlQuery };
        var result = await client.QueryByWiqlAsync(wiql, project, top: top);

        if (result.WorkItems == null || !result.WorkItems.Any())
        {
            return Enumerable.Empty<WorkItem>();
        }

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
        return workItems ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "get_work_items_by_ids")]
    [Description("Get multiple work items by their IDs in a single request.")]
    public async Task<IEnumerable<WorkItem>> GetWorkItemsByIds(
        [Description("Array of work item IDs to retrieve.")] int[] ids,
        [Description("The project name or ID.")] string project,
        [Description("Expand level: 'None', 'Relations', 'Fields', 'Links', 'All'.")]
            string expand = "Fields"
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();

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

    [McpServerTool(Name = "search_work_items")]
    [Description("Search for work items by text across title, description, and other fields.")]
    public async Task<IEnumerable<WorkItem>> SearchWorkItems(
        [Description("The search text to find in work items.")] string searchText,
        [Description("The project name or ID.")] string project,
        [Description("Filter by work item type (e.g., 'Bug', 'Task', 'User Story').")]
            string workItemType = "",
        [Description("Filter by state (e.g., 'Active', 'Closed', 'New').")] string state = "",
        [Description("Filter by assigned to (user display name or email).")] string assignedTo = "",
        [Description("Maximum number of results to return.")] int top = 50
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();

        var conditions = new List<string>();
        conditions.Add($"[System.TeamProject] = '{project}'");

        if (!string.IsNullOrEmpty(workItemType))
        {
            conditions.Add($"[System.WorkItemType] = '{workItemType}'");
        }

        if (!string.IsNullOrEmpty(state))
        {
            conditions.Add($"[System.State] = '{state}'");
        }

        if (!string.IsNullOrEmpty(assignedTo))
        {
            conditions.Add($"[System.AssignedTo] CONTAINS '{assignedTo}'");
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            conditions.Add(
                $"([System.Title] CONTAINS '{searchText}' OR [System.Description] CONTAINS '{searchText}')"
            );
        }

        var wiqlQuery =
            $"SELECT [System.Id], [System.Title], [System.State], [System.AssignedTo], [System.WorkItemType] FROM WorkItems WHERE {string.Join(" AND ", conditions)} ORDER BY [System.ChangedDate] DESC";

        var wiql = new Wiql { Query = wiqlQuery };
        var result = await client.QueryByWiqlAsync(wiql, project, top: top);

        if (result.WorkItems == null || !result.WorkItems.Any())
        {
            return Enumerable.Empty<WorkItem>();
        }

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
        return workItems ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "get_work_item_types")]
    [Description("Get all work item types available in a project.")]
    public async Task<IEnumerable<WorkItemType>> GetWorkItemTypes(
        [Description("The project name or ID.")] string project
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
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
        [Description("The project name or ID.")] string project,
        [Description("Maximum number of revisions to return.")] int top = 50
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();
        var revisions = await client.GetRevisionsAsync(project, id, top: top);
        return revisions ?? Enumerable.Empty<WorkItem>();
    }

    [McpServerTool(Name = "get_saved_queries")]
    [Description("Get saved queries (My Queries or Shared Queries) in a project.")]
    public async Task<IEnumerable<QueryHierarchyItem>> GetSavedQueries(
        [Description("The project name or ID.")] string project,
        [Description("Depth of folder expansion (0=no expansion).")] int depth = 1,
        [Description("Expand level: 'None', 'Wiql', 'Clauses', 'All', 'Minimal'.")]
            string expand = "None"
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();

        QueryExpand expandEnum = expand.ToLower() switch
        {
            "wiql" => QueryExpand.Wiql,
            "clauses" => QueryExpand.Clauses,
            "all" => QueryExpand.All,
            "minimal" => QueryExpand.Minimal,
            _ => QueryExpand.None,
        };

        var queries = await client.GetQueriesAsync(project, expandEnum, depth);
        return queries ?? Enumerable.Empty<QueryHierarchyItem>();
    }

    [McpServerTool(Name = "run_saved_query")]
    [Description("Execute a saved query by its ID and return the results.")]
    public async Task<IEnumerable<WorkItem>> RunSavedQuery(
        [Description("The project name or ID.")] string project,
        [Description("The ID of the saved query.")] Guid queryId,
        [Description("Maximum number of results to return.")] int top = 200
    )
    {
        var client = await _adoService.GetWorkItemTrackingApiAsync();

        var result = await client.QueryByIdAsync(project, queryId, top: top);

        if (result.WorkItems == null || !result.WorkItems.Any())
        {
            return Enumerable.Empty<WorkItem>();
        }

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await client.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);
        return workItems ?? Enumerable.Empty<WorkItem>();
    }
}