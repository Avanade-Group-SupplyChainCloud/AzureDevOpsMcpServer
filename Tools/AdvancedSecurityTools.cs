using AzureDevOpsMcpServer.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace AzureDevOpsMcpServer.Tools;

public static class AdvancedSecurityTools
{
    [McpServerTool(Name = "get_alerts")]
    [Description("Retrieve Advanced Security alerts for a repository.")]
    public static async Task<string> GetAlerts(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Repository name or ID")] string repository,
        [Description("Filter alerts by type (e.g., 'dependency', 'secret', 'code')")] string? alertType = null,
        [Description("Filter alerts by state (e.g., 'active', 'dismissed', 'fixed')")] string? state = null,
        [Description("Filter alerts by severity (e.g., 'critical', 'high', 'medium', 'low')")] string? severity = null,
        [Description("Maximum number of alerts to return")] int? top = null)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/alert/repositories/{repository}/alerts?api-version=7.1-preview.1";
        
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(alertType)) queryParams.Add($"alertType={alertType}");
        if (!string.IsNullOrEmpty(state)) queryParams.Add($"states={state}");
        if (!string.IsNullOrEmpty(severity)) queryParams.Add($"severities={severity}");
        if (top.HasValue) queryParams.Add($"$top={top}");
        
        if (queryParams.Any())
            url += "&" + string.Join("&", queryParams);

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error retrieving alerts: {response.StatusCode} - {content}";
            
        return content;
    }

    [McpServerTool(Name = "get_alert_details")]
    [Description("Get detailed information about a specific Advanced Security alert.")]
    public static async Task<string> GetAlertDetails(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Repository name or ID")] string repository,
        [Description("Alert ID")] int alertId)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/alert/repositories/{repository}/alerts/{alertId}?api-version=7.1-preview.1";

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error retrieving alert details: {response.StatusCode} - {content}";
            
        return content;
    }
}
