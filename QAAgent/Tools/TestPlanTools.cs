using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.QA.Tools;

[McpServerToolType]
public class TestPlanTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    private string BaseUrl => _adoService.Connection.Uri.ToString().TrimEnd('/');

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private async Task<int> GetRootSuiteId(string project, int planId)
    {
        var planJson = await GetTestPlan(project, planId);
        using var doc = JsonDocument.Parse(planJson);

        if (
            doc.RootElement.TryGetProperty("rootSuite", out var rootSuite)
            && rootSuite.ValueKind == JsonValueKind.Object
            && rootSuite.TryGetProperty("id", out var id)
            && id.TryGetInt32(out var suiteId)
        )
        {
            return suiteId;
        }

        // Fallback: some orgs/projects may return 'RootSuite' or omit it.
        if (
            doc.RootElement.TryGetProperty("RootSuite", out var rootSuiteAlt)
            && rootSuiteAlt.ValueKind == JsonValueKind.Object
            && rootSuiteAlt.TryGetProperty("id", out var idAlt)
            && idAlt.TryGetInt32(out var suiteIdAlt)
        )
        {
            return suiteIdAlt;
        }

        throw new InvalidOperationException(
            "Could not determine root suite id from test plan response."
        );
    }

    [McpServerTool(Name = "list_test_plans")]
    [Description("List test plans in a project.")]
    public async Task<string> ListTestPlans(
        [Description("The project name or ID.")] string project,
        [Description("Maximum number of plans to return.")] int top = 50,
        [Description("Number of plans to skip.")] int skip = 0
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/plans?api-version=7.1&$top={top}&$skip={skip}";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing test plans: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "get_test_plan")]
    [Description("Get a test plan by ID.")]
    public async Task<string> GetTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/plans/{planId}?api-version=7.1";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error getting test plan: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "create_test_plan")]
    [Description("Create a new test plan in a project.")]
    public async Task<string> CreateTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The plan name.")] string name,
        [Description("Optional description.")] string description = "",
        [Description("Optional area path.")] string areaPath = "",
        [Description("Optional iteration path.")] string iterationPath = "",
        [Description("Optional start date (ISO 8601). Example: 2025-12-19T00:00:00Z")] string startDate = "",
        [Description("Optional end date (ISO 8601). Example: 2025-12-31T23:59:59Z")] string endDate = ""
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/plans?api-version=7.1";

        var body = new Dictionary<string, object>
        {
            ["name"] = name,
        };

        if (!string.IsNullOrEmpty(description))
            body["description"] = description;
        if (!string.IsNullOrEmpty(areaPath))
            body["areaPath"] = areaPath;
        if (!string.IsNullOrEmpty(iterationPath))
            body["iteration"] = iterationPath;
        if (DateTimeOffset.TryParse(startDate, out var start))
            body["startDate"] = start.UtcDateTime;
        if (DateTimeOffset.TryParse(endDate, out var end))
            body["endDate"] = end.UtcDateTime;

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error creating test plan: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "update_test_plan")]
    [Description("Update an existing test plan.")]
    public async Task<string> UpdateTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("Optional new plan name.")] string name = "",
        [Description("Optional new description.")] string description = "",
        [Description("Optional new area path.")] string areaPath = "",
        [Description("Optional new iteration path.")] string iterationPath = "",
        [Description("Optional start date (ISO 8601).")] string startDate = "",
        [Description("Optional end date (ISO 8601).")] string endDate = ""
    )
    {
        // Update uses PATCH with a JSON body (not JSON patch).
        var url = $"{BaseUrl}/{project}/_apis/test/plans/{planId}?api-version=7.1";

        var body = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (!string.IsNullOrEmpty(description))
            body["description"] = description;
        if (!string.IsNullOrEmpty(areaPath))
            body["areaPath"] = areaPath;
        if (!string.IsNullOrEmpty(iterationPath))
            body["iteration"] = iterationPath;
        if (DateTimeOffset.TryParse(startDate, out var start))
            body["startDate"] = start.UtcDateTime;
        if (DateTimeOffset.TryParse(endDate, out var end))
            body["endDate"] = end.UtcDateTime;

        using var httpClient = _adoService.CreateHttpClient();
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error updating test plan: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "delete_test_plan")]
    [Description("Delete a test plan.")]
    public async Task<string> DeleteTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/plans/{planId}?api-version=7.1";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error deleting test plan: {response.StatusCode} - {content}";

        return $"Deleted test plan {planId}.";
    }

    [McpServerTool(Name = "list_test_suites")]
    [Description("List test suites under a test plan. Optionally filter by parent suite.")]
    public async Task<string> ListTestSuites(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("Optional parent suite ID. If omitted, returns root-level suites.")]
            int? parentSuiteId = null
    )
    {
        // List children suites for a given parent suite.
        var parentId = parentSuiteId ?? await GetRootSuiteId(project, planId);
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{parentId}/suites?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error listing test suites: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "get_test_suite")]
    [Description("Get a test suite by ID.")]
    public async Task<string> GetTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}?api-version=7.1";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error getting test suite: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "create_static_test_suite")]
    [Description("Create a static test suite under a plan (and optional parent suite).")]
    public async Task<string> CreateStaticTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite name.")] string name,
        [Description("Optional parent suite ID. If omitted, creates under the plan root.")] int? parentSuiteId = null
    )
    {
        var parentId = parentSuiteId ?? await GetRootSuiteId(project, planId);
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{parentId}?api-version=7.1";

        var body = new
        {
            name,
            suiteType = "StaticTestSuite",
        };

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error creating static suite: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "create_requirement_based_suite")]
    [Description("Create a requirement-based suite under a plan for a given work item (e.g., User Story).")]
    public async Task<string> CreateRequirementBasedSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite name.")] string name,
        [Description("The requirement work item ID.")] int requirementId,
        [Description("Optional parent suite ID. If omitted, creates under the plan root.")] int? parentSuiteId = null
    )
    {
        var parentId = parentSuiteId ?? await GetRootSuiteId(project, planId);
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{parentId}?api-version=7.1";

        var body = new
        {
            name,
            suiteType = "RequirementTestSuite",
            requirementId,
        };

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error creating requirement-based suite: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "delete_test_suite")]
    [Description("Delete a test suite from a plan.")]
    public async Task<string> DeleteTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}?api-version=7.1";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return $"Error deleting test suite: {response.StatusCode} - {content}";

        return $"Deleted test suite {suiteId} from plan {planId}.";
    }

    [McpServerTool(Name = "list_test_points")]
    [Description("List test points for a plan and suite.")]
    public async Task<string> ListTestPoints(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Maximum number of points to return.")] int top = 100,
        [Description("Number of points to skip.")] int skip = 0
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/points?api-version=7.1&$top={top}&$skip={skip}";
        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing test points: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "update_test_points")]
    [Description("Update fields on test points via JSON patch. Input is JSON array of patch operations.")]
    public async Task<string> UpdateTestPoints(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("JSON patch for test points updates. Example: [{\"op\":\"add\",\"path\":\"/fields/System.AssignedTo\",\"value\":\"user@contoso.com\"}]")]
            string patchJson,
        [Description("Optional comma-separated test point IDs to update. If omitted, updates all points returned by query.")]
            string pointIdsCsv = ""
    )
    {
        // The SDK has limited patch support for points; we do REST for flexibility.
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/points?api-version=7.1";

        if (!string.IsNullOrEmpty(pointIdsCsv))
        {
            url += $"&pointIds={Uri.EscapeDataString(pointIdsCsv)}";
        }

        using var httpClient = _adoService.CreateHttpClient();
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json"),
        };

        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return $"Error updating test points: {response.StatusCode} - {content}";
        }

        return content;
    }
}
