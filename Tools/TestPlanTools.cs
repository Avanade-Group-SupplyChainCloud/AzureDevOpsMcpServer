using AzureDevOpsMcpServer.Services;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace AzureDevOpsMcpServer.Tools;

public static class TestPlanTools
{
    [McpServerTool(Name = "list_test_plans")]
    [Description("List all test plans in a project.")]
    public static async Task<string> ListTestPlans(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Include only active plans")] bool activeOnly = false,
        [Description("Maximum results to return")] int top = 100)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/testplan/plans?api-version=7.1&$top={top}";
        
        if (activeOnly)
            url += "&filterActivePlans=true";

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error listing test plans: {response.StatusCode} - {content}";
            
        return content;
    }

    [McpServerTool(Name = "get_test_plan")]
    [Description("Get details of a specific test plan.")]
    public static async Task<string> GetTestPlan(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/testplan/plans/{planId}?api-version=7.1";

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error getting test plan: {response.StatusCode} - {content}";
            
        return content;
    }

    [McpServerTool(Name = "create_test_plan")]
    [Description("Create a new test plan in a project.")]
    public static async Task<string> CreateTestPlan(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Name of the test plan")] string name,
        [Description("Area path")] string? areaPath = null,
        [Description("Iteration path")] string? iteration = null,
        [Description("Description of the test plan")] string? description = null,
        [Description("Start date (ISO format)")] DateTime? startDate = null,
        [Description("End date (ISO format)")] DateTime? endDate = null)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/testplan/plans?api-version=7.1";

        var body = new Dictionary<string, object> { { "name", name } };
        if (!string.IsNullOrEmpty(areaPath)) body["areaPath"] = areaPath;
        if (!string.IsNullOrEmpty(iteration)) body["iteration"] = iteration;
        if (!string.IsNullOrEmpty(description)) body["description"] = description;
        if (startDate.HasValue) body["startDate"] = startDate.Value.ToString("o");
        if (endDate.HasValue) body["endDate"] = endDate.Value.ToString("o");

        using var httpClient = adoService.CreateHttpClient();
        
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error creating test plan: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "list_test_suites")]
    [Description("List all test suites in a test plan.")]
    public static async Task<string> ListTestSuites(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Expand children suites")] bool expand = false)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var suites = await client.GetTestSuitesForPlanAsync(project, planId, expand ? (int)SuiteExpand.Children : (int?)null);
        return JsonSerializer.Serialize(suites, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_suite")]
    [Description("Get details of a specific test suite.")]
    public static async Task<string> GetTestSuite(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Test suite ID")] int suiteId)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var suite = await client.GetTestSuiteByIdAsync(project, planId, suiteId);
        return JsonSerializer.Serialize(suite, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "create_test_suite")]
    [Description("Create a new test suite in a test plan.")]
    public static async Task<string> CreateTestSuite(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Name of the test suite")] string name,
        [Description("Parent suite ID")] int parentSuiteId,
        [Description("Suite type: 'StaticTestSuite', 'DynamicTestSuite', or 'RequirementTestSuite'")] string suiteType = "StaticTestSuite",
        [Description("Query string for dynamic suites")] string? queryString = null,
        [Description("Requirement ID for requirement-based suites")] int? requirementId = null)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/testplan/Plans/{planId}/suites?api-version=7.1";

        var body = new Dictionary<string, object>
        {
            { "name", name },
            { "parentSuite", new { id = parentSuiteId } },
            { "suiteType", suiteType }
        };

        if (!string.IsNullOrEmpty(queryString) && suiteType == "DynamicTestSuite")
            body["queryString"] = queryString;

        if (requirementId.HasValue && suiteType == "RequirementTestSuite")
            body["requirementId"] = requirementId.Value;

        using var httpClient = adoService.CreateHttpClient();
        
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error creating test suite: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "list_test_cases")]
    [Description("List all test cases in a test suite.")]
    public static async Task<string> ListTestCases(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Test suite ID")] int suiteId)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var cases = await client.GetTestCasesAsync(project, planId, suiteId);
        return JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "add_test_cases_to_suite")]
    [Description("Add existing test cases to a test suite.")]
    public static async Task<string> AddTestCasesToSuite(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Test suite ID")] int suiteId,
        [Description("Comma-separated list of test case IDs to add")] string testCaseIds)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/testplan/Plans/{planId}/Suites/{suiteId}/TestCase?api-version=7.1";

        var ids = testCaseIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => new { workItem = new { id = int.Parse(id.Trim()) } })
            .ToArray();

        using var httpClient = adoService.CreateHttpClient();
        
        var content = new StringContent(JsonSerializer.Serialize(ids), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error adding test cases: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "create_test_case")]
    [Description("Create a new test case work item.")]
    public static async Task<string> CreateTestCase(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Title of the test case")] string title,
        [Description("Steps in format: '1. Step one|Expected result one\\n2. Step two|Expected result two'")] string? steps = null,
        [Description("Area path")] string? areaPath = null,
        [Description("Iteration path")] string? iterationPath = null,
        [Description("Priority (1-4)")] int? priority = null,
        [Description("Assigned to (user email or ID)")] string? assignedTo = null)
    {
        var patchDocument = new List<object>
        {
            new { op = "add", path = "/fields/System.Title", value = title }
        };

        if (!string.IsNullOrEmpty(areaPath))
            patchDocument.Add(new { op = "add", path = "/fields/System.AreaPath", value = areaPath });

        if (!string.IsNullOrEmpty(iterationPath))
            patchDocument.Add(new { op = "add", path = "/fields/System.IterationPath", value = iterationPath });

        if (priority.HasValue)
            patchDocument.Add(new { op = "add", path = "/fields/Microsoft.VSTS.Common.Priority", value = priority.Value });

        if (!string.IsNullOrEmpty(assignedTo))
            patchDocument.Add(new { op = "add", path = "/fields/System.AssignedTo", value = assignedTo });

        if (!string.IsNullOrEmpty(steps))
        {
            var stepsXml = ConvertStepsToXml(steps);
            patchDocument.Add(new { op = "add", path = "/fields/Microsoft.VSTS.TCM.Steps", value = stepsXml });
        }

        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/wit/workitems/$Test%20Case?api-version=7.1";

        using var httpClient = adoService.CreateHttpClient();
        
        var content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json");
        var response = await httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error creating test case: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "update_test_case_steps")]
    [Description("Update the steps of an existing test case.")]
    public static async Task<string> UpdateTestCaseSteps(
        AzureDevOpsService adoService,
        [Description("Test case work item ID")] int id,
        [Description("Steps in format: '1. Step one|Expected result one\\n2. Step two|Expected result two'. Use '|' as delimiter between step and expected result.")] string steps)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/_apis/wit/workitems/{id}?api-version=7.1";

        var stepsXml = ConvertStepsToXml(steps);
        var patchDocument = new[] { new { op = "add", path = "/fields/Microsoft.VSTS.TCM.Steps", value = stepsXml } };

        using var httpClient = adoService.CreateHttpClient();
        
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json")
        };
        
        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error updating test case steps: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    private static string ConvertStepsToXml(string steps)
    {
        var sb = new StringBuilder("<steps id=\"0\" last=\"");
        var lines = steps.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        sb.Append(lines.Length.ToString());
        sb.Append("\">");
        
        var stepId = 1;
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            var stepText = parts[0].TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ');
            var expectedResult = parts.Length > 1 ? parts[1].Trim() : "";
            
            sb.Append($"<step id=\"{stepId}\" type=\"ActionStep\">");
            sb.Append($"<parameterizedString isFormatted=\"true\">&lt;DIV&gt;&lt;P&gt;{System.Security.SecurityElement.Escape(stepText)}&lt;/P&gt;&lt;/DIV&gt;</parameterizedString>");
            sb.Append($"<parameterizedString isFormatted=\"true\">&lt;DIV&gt;&lt;P&gt;{System.Security.SecurityElement.Escape(expectedResult)}&lt;/P&gt;&lt;/DIV&gt;</parameterizedString>");
            sb.Append("</step>");
            stepId++;
        }
        
        sb.Append("</steps>");
        return sb.ToString();
    }

    [McpServerTool(Name = "list_test_runs")]
    [Description("List test runs in a project.")]
    public static async Task<string> ListTestRuns(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Optional build URI to filter by")] string? buildUri = null,
        [Description("Maximum number of runs to return")] int top = 50)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var runs = await client.GetTestRunsAsync(project, buildUri: buildUri, top: top);
        return JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_run")]
    [Description("Get details of a specific test run.")]
    public static async Task<string> GetTestRun(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test run ID")] int runId)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var run = await client.GetTestRunByIdAsync(project, runId);
        return JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_results")]
    [Description("Get test results for a test run.")]
    public static async Task<string> GetTestResults(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test run ID")] int runId,
        [Description("Filter by outcome: 'Passed', 'Failed', 'NotExecuted', etc.")] string? outcomeFilter = null)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var results = await client.GetTestResultsAsync(project, runId);
        
        var filtered = results ?? new List<TestCaseResult>();
        if (!string.IsNullOrEmpty(outcomeFilter))
        {
            filtered = filtered.Where(r => r.Outcome?.Equals(outcomeFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }
        
        return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_result_details")]
    [Description("Get detailed information about a specific test result.")]
    public static async Task<string> GetTestResultDetails(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test run ID")] int runId,
        [Description("Test result ID")] int testResultId)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var results = await client.GetTestResultsAsync(project, runId);
        var result = results.FirstOrDefault(r => r.Id == testResultId);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_results_from_build")]
    [Description("Get test results associated with a specific build.")]
    public static async Task<string> GetTestResultsFromBuild(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Build ID")] int buildId)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version=7.1";

        using var httpClient = adoService.CreateHttpClient();
        
        var response = await httpClient.GetAsync(url);
        var runsContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error getting test runs for build: {response.StatusCode} - {runsContent}";

        var runsDoc = JsonDocument.Parse(runsContent);
        var allResults = new List<object>();

        if (runsDoc.RootElement.TryGetProperty("value", out var runs))
        {
            foreach (var run in runs.EnumerateArray())
            {
                if (run.TryGetProperty("id", out var runIdProp))
                {
                    var runId = runIdProp.GetInt32();
                    var resultsUrl = $"{baseUrl}/{project}/_apis/test/runs/{runId}/results?api-version=7.1";
                    var resultsResponse = await httpClient.GetAsync(resultsUrl);
                    var resultsContent = await resultsResponse.Content.ReadAsStringAsync();
                    
                    if (resultsResponse.IsSuccessStatusCode)
                    {
                        var resultsDoc = JsonDocument.Parse(resultsContent);
                        if (resultsDoc.RootElement.TryGetProperty("value", out var results))
                        {
                            foreach (var result in results.EnumerateArray())
                            {
                                allResults.Add(new
                                {
                                    runId,
                                    result = JsonSerializer.Deserialize<object>(result.GetRawText())
                                });
                            }
                        }
                    }
                }
            }
        }

        return JsonSerializer.Serialize(allResults, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_test_points")]
    [Description("Get test points for a test suite.")]
    public static async Task<string> GetTestPoints(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Test suite ID")] int suiteId)
    {
        var client = await adoService.GetTestManagementApiAsync();
        var points = await client.GetPointsAsync(project, planId, suiteId);
        return JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "create_test_run")]
    [Description("Create a new test run.")]
    public static async Task<string> CreateTestRun(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Name of the test run")] string name,
        [Description("Test plan ID")] int planId,
        [Description("Comma-separated list of test point IDs to include")] string testPointIds,
        [Description("Optional build ID to associate")] int? buildId = null,
        [Description("Optional comment")] string? comment = null)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs?api-version=7.1";

        var pointIds = testPointIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.Parse(id.Trim()))
            .ToArray();

        var body = new Dictionary<string, object>
        {
            { "name", name },
            { "plan", new { id = planId } },
            { "pointIds", pointIds }
        };

        if (buildId.HasValue)
            body["build"] = new { id = buildId.Value };
        if (!string.IsNullOrEmpty(comment))
            body["comment"] = comment;

        using var httpClient = adoService.CreateHttpClient();
        
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error creating test run: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "update_test_results")]
    [Description("Update test results (set outcome, error message, etc.).")]
    public static async Task<string> UpdateTestResults(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test run ID")] int runId,
        [Description("JSON array of result updates: [{id, outcome, errorMessage, comment}]")] string resultsJson)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs/{runId}/results?api-version=7.1";

        using var httpClient = adoService.CreateHttpClient();
        
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(resultsJson, Encoding.UTF8, "application/json")
        };
        
        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error updating test results: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }

    [McpServerTool(Name = "complete_test_run")]
    [Description("Complete a test run (change state to Completed).")]
    public static async Task<string> CompleteTestRun(
        AzureDevOpsService adoService,
        [Description("Project name or ID")] string project,
        [Description("Test run ID")] int runId)
    {
        var connection = adoService.Connection;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/runs/{runId}?api-version=7.1";

        var body = new { state = "Completed" };

        using var httpClient = adoService.CreateHttpClient();
        
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        
        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            return $"Error completing test run: {response.StatusCode} - {responseContent}";
            
        return responseContent;
    }
}
