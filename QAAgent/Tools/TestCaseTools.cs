using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.QA.Tools;

[McpServerToolType]
public class TestCaseTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    private string BaseUrl => _adoService.Connection.Uri.ToString().TrimEnd('/');

    [McpServerTool(Name = "create_test_case")]
    [Description("Create a new Test Case work item.")]
    public async Task<WorkItem> CreateTestCase(
        [Description("The project name or ID.")] string project,
        [Description("Test case title.")] string title,
        [Description("Optional area path.")] string areaPath = "",
        [Description("Optional iteration path.")] string iterationPath = "",
        [Description("Optional priority (1-4).")] int? priority = null,
        [Description("Optional automated test name.")] string automatedTestName = "",
        [Description("Optional automated test storage.")] string automatedTestStorage = "",
        [Description("Optional additional fields: dictionary of field reference name -> value.")]
            Dictionary<string, object> additionalFields = null
    )
    {
        var wit = await _adoService.GetWorkItemTrackingApiAsync();

        var patch = new JsonPatchDocument
        {
            new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Title",
                Value = title,
            },
        };

        if (!string.IsNullOrEmpty(areaPath))
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.AreaPath",
                    Value = areaPath,
                }
            );
        }

        if (!string.IsNullOrEmpty(iterationPath))
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.IterationPath",
                    Value = iterationPath,
                }
            );
        }

        if (priority.HasValue)
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Common.Priority",
                    Value = priority.Value,
                }
            );
        }

        if (!string.IsNullOrEmpty(automatedTestName))
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.TCM.AutomatedTestName",
                    Value = automatedTestName,
                }
            );
        }

        if (!string.IsNullOrEmpty(automatedTestStorage))
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.TCM.AutomatedTestStorage",
                    Value = automatedTestStorage,
                }
            );
        }

        if (additionalFields != null)
        {
            foreach (var kvp in additionalFields)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                patch.Add(
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{kvp.Key}",
                        Value = kvp.Value,
                    }
                );
            }
        }

        return await wit.CreateWorkItemAsync(patch, project, "Test Case");
    }

    [McpServerTool(Name = "update_test_case")]
    [Description("Update fields on a Test Case work item.")]
    public async Task<WorkItem> UpdateTestCase(
        [Description("The project name or ID.")] string project,
        [Description("Test case work item ID.")] int id,
        [Description("Field updates as dictionary of field reference name -> value.")]
            Dictionary<string, object> updates
    )
    {
        var wit = await _adoService.GetWorkItemTrackingApiAsync();
        var patch = new JsonPatchDocument();

        foreach (var kvp in updates)
        {
            patch.Add(
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = $"/fields/{kvp.Key}",
                    Value = kvp.Value,
                }
            );
        }

        return await wit.UpdateWorkItemAsync(patch, id);
    }

    [McpServerTool(Name = "get_test_case")]
    [Description("Get a Test Case work item by ID.")]
    public async Task<WorkItem> GetTestCase(
        [Description("The project name or ID.")] string project,
        [Description("Test case work item ID.")] int id
    )
    {
        var wit = await _adoService.GetWorkItemTrackingApiAsync();
        return await wit.GetWorkItemAsync(project, id, expand: WorkItemExpand.Fields);
    }

    [McpServerTool(Name = "add_test_cases_to_suite")]
    [Description("Add existing Test Case work items to a test suite.")]
    public async Task<string> AddTestCasesToSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Test case work item IDs to add.")] IEnumerable<int> testCaseIds
    )
    {
        // REST API: POST .../Suites/{suiteId}/testcases/{testCaseId}
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        using var httpClient = _adoService.CreateHttpClient();

        var results = new List<object>();

        foreach (var testCaseId in testCaseIds ?? Enumerable.Empty<int>())
        {
            var url = $"{baseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/testcases/{testCaseId}?api-version=7.1";
            var response = await httpClient.PostAsync(url, content: null);
            var content = await response.Content.ReadAsStringAsync();

            results.Add(
                new
                {
                    testCaseId,
                    status = response.StatusCode.ToString(),
                    success = response.IsSuccessStatusCode,
                    response = content,
                }
            );
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "remove_test_case_from_suite")]
    [Description("Remove a Test Case from a suite.")]
    public async Task<string> RemoveTestCaseFromSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("The test case ID.")] int testCaseId
    )
    {
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/testcases/{testCaseId}?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error removing test case from suite: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "list_test_cases_in_suite")]
    [Description("List test cases in a suite.")]
    public async Task<string> ListTestCasesInSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Maximum number of items to return.")] int top = 100,
        [Description("Number of items to skip.")] int skip = 0
    )
    {
        var url = $"{BaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/testcases?api-version=7.1&$top={top}&$skip={skip}";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error listing test cases in suite: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "create_test_case_result")]
    [Description("Create test case results for a run. Useful for automated publishing.")]
    public async Task<string> CreateTestCaseResults(
        [Description("The project name or ID.")] string project,
        [Description("The test run ID.")] int runId,
        [Description("JSON array of results matching Azure DevOps Test Case Result schema.")]
            string resultsJson
    )
    {
        // Use REST directly to avoid tight coupling to SDK contracts.
        var baseUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var url = $"{baseUrl}/{project}/_apis/test/Runs/{runId}/results?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.PostAsync(
            url,
            new StringContent(resultsJson, System.Text.Encoding.UTF8, "application/json")
        );

        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return $"Error creating test case results: {response.StatusCode} - {content}";
        }

        return content;
    }
}
