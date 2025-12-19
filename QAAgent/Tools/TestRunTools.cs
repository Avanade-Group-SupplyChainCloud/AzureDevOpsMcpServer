using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.TestManagement.WebApi.Contracts;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.QA.Tools;

[McpServerToolType]
public class TestRunTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_test_runs")]
    [Description("List test runs in a project.")]
    public async Task<string> ListTestRuns(
        [Description("The project name or ID.")] string project,
        [Description("Maximum number of runs to return.")] int top = 50,
        [Description("Number of runs to skip.")] int skip = 0,
        [Description("Optional run state filter: 'InProgress', 'Completed', etc.")]
            string state = ""
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs?api-version=7.1&$top={top}&$skip={skip}";

        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing test runs: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "get_test_run")]
    [Description("Get a test run by ID.")]
    public async Task<string> GetTestRun(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs/{runId}?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error getting test run: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "create_test_run")]
    [Description("Create a new test run.")]
    public async Task<string> CreateTestRun(
        [Description("The project name or ID.")] string project,
        [Description("Test run name.")] string name,
        [Description("Test plan ID.")] int planId,
        [Description("Test suite ID.")] int suiteId,
        [Description("Optional build ID associated with run.")] int? buildId = null,
        [Description("Optional comment.")] string comment = ""
    )
    {
        // REST to keep it flexible
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs?api-version=7.1";

        var body = new
        {
            name,
            plan = new { id = planId },
            pointIds = Array.Empty<int>(),
            comment = string.IsNullOrEmpty(comment) ? null : comment,
            build = buildId.HasValue ? new { id = buildId.Value } : null,
            state = "InProgress",
            suite = new { id = suiteId },
        };

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error creating test run: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "update_test_run_state")]
    [Description("Update test run state (e.g., InProgress -> Completed).")]
    public async Task<string> UpdateTestRunState(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId,
        [Description("New state. Typically 'InProgress' or 'Completed'.")] string state,
        [Description("Optional comment.")] string comment = ""
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs/{runId}?api-version=7.1";

        var patch = new object[]
        {
            new { op = "add", path = "/state", value = state },
        };

        if (!string.IsNullOrEmpty(comment))
        {
            patch =
            [
                new { op = "add", path = "/state", value = state },
                new { op = "add", path = "/comment", value = comment },
            ];
        }

        using var httpClient = _adoService.CreateHttpClient();
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(patch),
                System.Text.Encoding.UTF8,
                "application/json-patch+json"
            ),
        };

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error updating test run: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "list_test_run_results")]
    [Description("List test results for a run.")]
    public async Task<string> ListTestRunResults(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId,
        [Description("Maximum number of results to return.")] int top = 100,
        [Description("Number of results to skip.")] int skip = 0
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Runs/{runId}/results?api-version=7.1&$top={top}&$skip={skip}";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing run results: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "list_test_run_attachments")]
    [Description("List attachments for a test run.")]
    public async Task<string> ListTestRunAttachments(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Runs/{runId}/attachments?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing run attachments: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "add_test_run_attachment")]
    [Description("Add an attachment to a test run. Content should be base64 encoded.")]
    public async Task<string> AddTestRunAttachment(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId,
        [Description("Attachment file name.")] string fileName,
        [Description("Base64 file content.")] string base64Content,
        [Description("Optional comment.")] string comment = ""
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Runs/{runId}/attachments?api-version=7.1";

        var body = new
        {
            fileName,
            comment = string.IsNullOrEmpty(comment) ? null : comment,
            stream = base64Content,
        };

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error adding run attachment: {response.StatusCode} - {content}";

        return content;
    }
}
