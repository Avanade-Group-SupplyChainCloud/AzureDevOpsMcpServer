using AzureDevOpsMcpServer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace AzureDevOpsMcpServer.Tools;

public static class SearchTools
{
    [McpServerTool(Name = "search_code")]
    [Description("Search Azure DevOps Repositories for code matching the search text.")]
    public static async Task<string> SearchCode(
        AzureDevOpsService adoService,
        [Description("Text to search for in code")] string searchText,
        [Description("Project name to scope the search (optional)")] string? project = null,
        [Description("Repository name to scope the search (optional)")] string? repository = null,
        [Description("Branch name to scope the search (optional)")] string? branch = null,
        [Description("Path to scope the search (optional)")] string? path = null,
        [Description("Number of results to skip")] int skip = 0,
        [Description("Maximum number of results to return")] int top = 100)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/_apis/search/codesearchresults?api-version=7.1-preview.1";

        var filters = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(project)) filters["Project"] = new List<string> { project };
        if (!string.IsNullOrEmpty(repository)) filters["Repository"] = new List<string> { repository };
        if (!string.IsNullOrEmpty(branch)) filters["Branch"] = new List<string> { branch };
        if (!string.IsNullOrEmpty(path)) filters["Path"] = new List<string> { path };

        var requestBody = new
        {
            searchText = searchText,
            filters = filters,
            skipResults = skip,
            takeResults = top
        };

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.PostAsJsonAsync(url, requestBody);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error searching code: {response.StatusCode} - {content}";
            
        return content;
    }

    [McpServerTool(Name = "search_wiki")]
    [Description("Search Azure DevOps Wiki for pages matching the search text.")]
    public static async Task<string> SearchWiki(
        AzureDevOpsService adoService,
        [Description("Text to search for in wiki")] string searchText,
        [Description("Project name to scope the search (optional)")] string? project = null,
        [Description("Wiki name to scope the search (optional)")] string? wiki = null,
        [Description("Number of results to skip")] int skip = 0,
        [Description("Maximum number of results to return")] int top = 100)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/_apis/search/wikisearchresults?api-version=7.1-preview.1";

        var filters = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(project)) filters["Project"] = new List<string> { project };
        if (!string.IsNullOrEmpty(wiki)) filters["Wiki"] = new List<string> { wiki };

        var requestBody = new
        {
            searchText = searchText,
            filters = filters,
            skipResults = skip,
            takeResults = top
        };

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.PostAsJsonAsync(url, requestBody);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error searching wiki: {response.StatusCode} - {content}";
            
        return content;
    }

    [McpServerTool(Name = "search_workitem")]
    [Description("Search Azure DevOps Work Items matching the search text.")]
    public static async Task<string> SearchWorkItem(
        AzureDevOpsService adoService,
        [Description("Text to search for in work items")] string searchText,
        [Description("Project name to scope the search (optional)")] string? project = null,
        [Description("Work item type filter (optional)")] string? workItemType = null,
        [Description("State filter (optional)")] string? state = null,
        [Description("Assigned to filter (optional)")] string? assignedTo = null,
        [Description("Area path filter (optional)")] string? areaPath = null,
        [Description("Number of results to skip")] int skip = 0,
        [Description("Maximum number of results to return")] int top = 100)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/_apis/search/workitemsearchresults?api-version=7.1-preview.1";

        var filters = new Dictionary<string, List<string>>();
        if (!string.IsNullOrEmpty(project)) filters["System.TeamProject"] = new List<string> { project };
        if (!string.IsNullOrEmpty(workItemType)) filters["System.WorkItemType"] = new List<string> { workItemType };
        if (!string.IsNullOrEmpty(state)) filters["System.State"] = new List<string> { state };
        if (!string.IsNullOrEmpty(assignedTo)) filters["System.AssignedTo"] = new List<string> { assignedTo };
        if (!string.IsNullOrEmpty(areaPath)) filters["System.AreaPath"] = new List<string> { areaPath };

        var requestBody = new
        {
            searchText = searchText,
            filters = filters,
            skipResults = skip,
            takeResults = top
        };

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.PostAsJsonAsync(url, requestBody);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error searching work items: {response.StatusCode} - {content}";
            
        return content;
    }
}
