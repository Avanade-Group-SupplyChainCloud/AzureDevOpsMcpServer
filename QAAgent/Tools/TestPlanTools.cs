using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.QA.Tools;

[McpServerToolType]
public class TestPlanTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_test_plans")]
    [Description("List test plans in a project.")]
    public async Task<IEnumerable<TestPlan>> ListTestPlans(
        [Description("The project name or ID.")] string project,
        [Description("Maximum number of plans to return.")] int top = 50,
        [Description("Number of plans to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        var plans = await client.GetPlansAsync(project, skip: skip, top: top);
#pragma warning restore CS0612
        return plans ?? Enumerable.Empty<TestPlan>();
    }

    [McpServerTool(Name = "get_test_plan")]
    [Description("Get a test plan by ID.")]
    public async Task<TestPlan> GetTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        return await client.GetPlanByIdAsync(project, planId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "create_test_plan")]
    [Description("Create a new test plan in a project.")]
    public async Task<TestPlan> CreateTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The plan name.")] string name,
        [Description("Optional description.")] string description = "",
        [Description("Optional area path.")] string areaPath = "",
        [Description("Optional iteration path.")] string iterationPath = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var planParams = new PlanUpdateModel(
            name: name,
            description: string.IsNullOrEmpty(description) ? null : description,
            area: string.IsNullOrEmpty(areaPath) ? null : new ShallowReference { Name = areaPath },
            iteration: string.IsNullOrEmpty(iterationPath) ? null : iterationPath
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.CreateTestPlanAsync(planParams, project);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "update_test_plan")]
    [Description("Update an existing test plan.")]
    public async Task<TestPlan> UpdateTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("Optional new plan name.")] string name = "",
        [Description("Optional new description.")] string description = "",
        [Description("Optional new area path.")] string areaPath = "",
        [Description("Optional new iteration path.")] string iterationPath = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var planParams = new PlanUpdateModel(
            name: string.IsNullOrEmpty(name) ? null : name,
            description: string.IsNullOrEmpty(description) ? null : description,
            area: string.IsNullOrEmpty(areaPath) ? null : new ShallowReference { Name = areaPath },
            iteration: string.IsNullOrEmpty(iterationPath) ? null : iterationPath
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.UpdateTestPlanAsync(planParams, project, planId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "delete_test_plan")]
    [Description("Delete a test plan.")]
    public async Task<string> DeleteTestPlan(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        await client.DeleteTestPlanAsync(project, planId);
#pragma warning restore CS0612
        return $"Deleted test plan {planId}.";
    }

    [McpServerTool(Name = "list_test_suites")]
    [Description("List test suites under a test plan.")]
    public async Task<IEnumerable<TestSuite>> ListTestSuites(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        var suites = await client.GetTestSuitesForPlanAsync(project, planId);
#pragma warning restore CS0612
        return suites ?? Enumerable.Empty<TestSuite>();
    }

    [McpServerTool(Name = "get_test_suite")]
    [Description("Get a test suite by ID.")]
    public async Task<TestSuite> GetTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        return await client.GetTestSuiteByIdAsync(project, planId, suiteId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "create_static_test_suite")]
    [Description("Create a static test suite under a plan.")]
    public async Task<IEnumerable<TestSuite>> CreateStaticTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite name.")] string name,
        [Description("Parent suite ID. Use the root suite ID from the test plan if creating at root level.")] int parentSuiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var suiteParams = new SuiteCreateModel(
            name: name,
            suiteType: "StaticTestSuite",
            queryString: null,
            requirementIds: null
        );

#pragma warning disable CS0612 // Type or member is obsolete
        var result = await client.CreateTestSuiteAsync(suiteParams, project, planId, parentSuiteId);
#pragma warning restore CS0612
        return result ?? Enumerable.Empty<TestSuite>();
    }

    [McpServerTool(Name = "create_requirement_based_suite")]
    [Description("Create a requirement-based suite under a plan for a given work item (e.g., User Story).")]
    public async Task<IEnumerable<TestSuite>> CreateRequirementBasedSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite name.")] string name,
        [Description("The requirement work item ID.")] int requirementId,
        [Description("Parent suite ID. Use the root suite ID from the test plan if creating at root level.")] int parentSuiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var suiteParams = new SuiteCreateModel(
            name: name,
            suiteType: "RequirementTestSuite",
            queryString: null,
            requirementIds: new[] { requirementId }
        );

#pragma warning disable CS0612 // Type or member is obsolete
        var result = await client.CreateTestSuiteAsync(suiteParams, project, planId, parentSuiteId);
#pragma warning restore CS0612
        return result ?? Enumerable.Empty<TestSuite>();
    }

    [McpServerTool(Name = "create_query_based_suite")]
    [Description("Create a query-based test suite under a plan.")]
    public async Task<IEnumerable<TestSuite>> CreateQueryBasedSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite name.")] string name,
        [Description("The WIQL query string for the suite.")] string queryString,
        [Description("Parent suite ID. Use the root suite ID from the test plan if creating at root level.")] int parentSuiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var suiteParams = new SuiteCreateModel(
            name: name,
            suiteType: "DynamicTestSuite",
            queryString: queryString,
            requirementIds: null
        );

#pragma warning disable CS0612 // Type or member is obsolete
        var result = await client.CreateTestSuiteAsync(suiteParams, project, planId, parentSuiteId);
#pragma warning restore CS0612
        return result ?? Enumerable.Empty<TestSuite>();
    }

    [McpServerTool(Name = "update_test_suite")]
    [Description("Update an existing test suite.")]
    public async Task<TestSuite> UpdateTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Optional new suite name.")] string name = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        var suiteParams = new SuiteUpdateModel(
            name: string.IsNullOrEmpty(name) ? null : name
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.UpdateTestSuiteAsync(suiteParams, project, planId, suiteId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "delete_test_suite")]
    [Description("Delete a test suite from a plan.")]
    public async Task<string> DeleteTestSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        await client.DeleteTestSuiteAsync(project, planId, suiteId);
#pragma warning restore CS0612
        return $"Deleted test suite {suiteId} from plan {planId}.";
    }

    [McpServerTool(Name = "list_test_points")]
    [Description("List test points for a plan and suite.")]
    public async Task<IEnumerable<TestPoint>> ListTestPoints(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Maximum number of points to return.")] int top = 100,
        [Description("Number of points to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        var points = await client.GetPointsAsync(project, planId, suiteId, top: top, skip: skip);
#pragma warning restore CS0612
        return points ?? Enumerable.Empty<TestPoint>();
    }

    [McpServerTool(Name = "update_test_points")]
    [Description("Update test points (e.g., assign tester, reset outcome).")]
    public async Task<IEnumerable<TestPoint>> UpdateTestPoints(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Comma-separated test point IDs to update.")] string pointIdsCsv,
        [Description("Optional tester ID to assign.")] string testerId = "",
        [Description("Set to true to reset the test point to active state.")] bool resetToActive = false
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();

        IdentityRef tester = null;
        if (!string.IsNullOrEmpty(testerId))
        {
            tester = new IdentityRef { Id = testerId };
        }

        var updateModel = new PointUpdateModel(
            resetToActive: resetToActive,
            tester: tester
        );

#pragma warning disable CS0612 // Type or member is obsolete
        var result = await client.UpdateTestPointsAsync(updateModel, project, planId, suiteId, pointIdsCsv);
#pragma warning restore CS0612
        return result ?? Enumerable.Empty<TestPoint>();
    }

    [McpServerTool(Name = "add_test_cases_to_suite")]
    [Description("Add existing test cases to a test suite.")]
    public async Task<IEnumerable<SuiteTestCase>> AddTestCasesToSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Comma-separated test case work item IDs to add.")] string testCaseIdsCsv
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        var result = await client.AddTestCasesToSuiteAsync(project, planId, suiteId, testCaseIdsCsv);
#pragma warning restore CS0612
        return result ?? Enumerable.Empty<SuiteTestCase>();
    }

    [McpServerTool(Name = "list_test_cases_in_suite")]
    [Description("List test cases in a suite.")]
    public async Task<IEnumerable<SuiteTestCase>> ListTestCasesInSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        var testCases = await client.GetTestCasesAsync(project, planId, suiteId);
#pragma warning restore CS0612
        return testCases ?? Enumerable.Empty<SuiteTestCase>();
    }

    [McpServerTool(Name = "remove_test_cases_from_suite")]
    [Description("Remove test cases from a suite.")]
    public async Task<string> RemoveTestCasesFromSuite(
        [Description("The project name or ID.")] string project,
        [Description("The test plan ID.")] int planId,
        [Description("The suite ID.")] int suiteId,
        [Description("Comma-separated test case IDs to remove.")] string testCaseIdsCsv
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
#pragma warning disable CS0612 // Type or member is obsolete
        await client.RemoveTestCasesFromSuiteUrlAsync(project, planId, suiteId, testCaseIdsCsv);
#pragma warning restore CS0612
        return $"Removed test cases {testCaseIdsCsv} from suite {suiteId}.";
    }
}
