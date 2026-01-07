using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.QA.Tools;

[McpServerToolType]
public class TestRunTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_test_runs")]
    [Description("List test runs.")]
    public async Task<IEnumerable<TestRun>> ListTestRuns(
        [Description("Maximum number of runs to return.")] int top = 50,
        [Description("Number of runs to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        var runs = await client.GetTestRunsAsync(project, top: top, skip: skip);
#pragma warning restore CS0612
        return runs ?? Enumerable.Empty<TestRun>();
    }

    [McpServerTool(Name = "get_test_run")]
    [Description("Get a test run by ID.")]
    public async Task<TestRun> GetTestRun(
        [Description("The test run ID.")] int runId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        return await client.GetTestRunByIdAsync(project, runId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "create_test_run")]
    [Description("Create a new test run.")]
    public async Task<TestRun> CreateTestRun(
        [Description("Test run name.")] string name,
        [Description("Test plan ID.")] int planId,
        [Description("Optional comment.")] string comment = "",
        [Description("Set to true to create as automated run.")] bool isAutomated = false
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var runCreateModel = new RunCreateModel(
            name: name,
            plan: new ShallowReference { Id = planId.ToString() },
            isAutomated: isAutomated,
            comment: string.IsNullOrEmpty(comment) ? null : comment
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.CreateTestRunAsync(runCreateModel, project);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "update_test_run")]
    [Description("Update test run properties (state, comment, name, etc.).")]
    public async Task<TestRun> UpdateTestRun(
        [Description("The test run ID.")] int runId,
        [Description("Optional new name.")] string name = "",
        [Description("Optional new state: 'InProgress', 'Completed', 'Aborted', 'Waiting'.")]
            string state = "",
        [Description("Optional comment.")] string comment = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var runUpdateModel = new RunUpdateModel(
            name: string.IsNullOrEmpty(name) ? null : name,
            state: string.IsNullOrEmpty(state) ? null : state,
            comment: string.IsNullOrEmpty(comment) ? null : comment
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.UpdateTestRunAsync(runUpdateModel, project, runId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "delete_test_run")]
    [Description("Delete a test run.")]
    public async Task<string> DeleteTestRun(
        [Description("The test run ID.")] int runId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        await client.DeleteTestRunAsync(project, runId);
#pragma warning restore CS0612
        return $"Deleted test run {runId}.";
    }

    [McpServerTool(Name = "list_test_run_results")]
    [Description("List test results for a run.")]
    public async Task<IEnumerable<TestCaseResult>> ListTestRunResults(
        [Description("The test run ID.")] int runId,
        [Description("Maximum number of results to return.")] int top = 100,
        [Description("Number of results to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        var results = await client.GetTestResultsAsync(project, runId, top: top, skip: skip);
#pragma warning restore CS0612
        return results ?? Enumerable.Empty<TestCaseResult>();
    }

    [McpServerTool(Name = "get_test_result")]
    [Description("Get a specific test result by ID.")]
    public async Task<TestCaseResult> GetTestResult(
        [Description("The test run ID.")] int runId,
        [Description("The test result ID.")] int testResultId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        var results = await client.GetTestResultByIdAsync(project, runId, testResultId);
#pragma warning restore CS0612
        return results;
    }

    [McpServerTool(Name = "add_test_results")]
    [Description("Add test results to a test run.")]
    public async Task<IEnumerable<TestCaseResult>> AddTestResults(
        [Description("The test run ID.")] int runId,
        [Description("Test case ID.")] int testCaseId,
        [Description("Outcome: 'Passed', 'Failed', 'Blocked', 'NotApplicable', etc.")]
            string outcome,
        [Description("Optional comment.")] string comment = "",
        [Description("Optional error message (for failed tests).")] string errorMessage = "",
        [Description("Optional duration in milliseconds.")] long? durationInMs = null
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var result = new TestCaseResult
        {
            TestCase = new ShallowReference { Id = testCaseId.ToString() },
            Outcome = outcome,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            ErrorMessage = string.IsNullOrEmpty(errorMessage) ? null : errorMessage,
        };

        if (durationInMs.HasValue)
        {
            result.DurationInMs = durationInMs.Value;
        }

#pragma warning disable CS0612 // Type or member is obsolete
        var results = await client.AddTestResultsToTestRunAsync(new[] { result }, project, runId);
#pragma warning restore CS0612
        return results ?? Enumerable.Empty<TestCaseResult>();
    }

    [McpServerTool(Name = "update_test_results")]
    [Description("Update existing test results.")]
    public async Task<IEnumerable<TestCaseResult>> UpdateTestResults(
        [Description("The test run ID.")] int runId,
        [Description("The test result ID to update.")] int testResultId,
        [Description("Optional new outcome: 'Passed', 'Failed', 'Blocked', 'NotApplicable', etc.")]
            string outcome = "",
        [Description("Optional comment.")] string comment = "",
        [Description("Optional error message.")] string errorMessage = "",
        [Description("Optional state: 'Pending', 'Completed'.")] string state = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var result = new TestCaseResult { Id = testResultId };

        if (!string.IsNullOrEmpty(outcome))
            result.Outcome = outcome;
        if (!string.IsNullOrEmpty(comment))
            result.Comment = comment;
        if (!string.IsNullOrEmpty(errorMessage))
            result.ErrorMessage = errorMessage;
        if (!string.IsNullOrEmpty(state))
            result.State = state;

#pragma warning disable CS0612 // Type or member is obsolete
        var results = await client.UpdateTestResultsAsync(new[] { result }, project, runId);
#pragma warning restore CS0612
        return results ?? Enumerable.Empty<TestCaseResult>();
    }

    [McpServerTool(Name = "get_test_run_statistics")]
    [Description("Get statistics for a test run.")]
    public async Task<TestRunStatistic> GetTestRunStatistics(
        [Description("The test run ID.")] int runId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        return await client.GetTestRunStatisticsAsync(project, runId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "list_test_run_attachments")]
    [Description("List attachments for a test run.")]
    public async Task<IEnumerable<TestAttachment>> ListTestRunAttachments(
        [Description("The test run ID.")] int runId
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;
#pragma warning disable CS0612 // Type or member is obsolete
        var attachments = await client.GetTestRunAttachmentsAsync(project, runId);
#pragma warning restore CS0612
        return attachments ?? Enumerable.Empty<TestAttachment>();
    }

    [McpServerTool(Name = "create_test_run_attachment")]
    [Description("Create an attachment for a test run.")]
    public async Task<TestAttachmentReference> CreateTestRunAttachment(
        [Description("The test run ID.")] int runId,
        [Description("Attachment file name.")] string fileName,
        [Description("Base64-encoded file content.")] string base64Content,
        [Description("Optional comment.")] string comment = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var attachment = new TestAttachmentRequestModel(
            stream: base64Content,
            fileName: fileName,
            comment: string.IsNullOrEmpty(comment) ? null : comment
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.CreateTestRunAttachmentAsync(attachment, project, runId);
#pragma warning restore CS0612
    }

    [McpServerTool(Name = "create_test_result_attachment")]
    [Description("Create an attachment for a test result.")]
    public async Task<TestAttachmentReference> CreateTestResultAttachment(
        [Description("The test run ID.")] int runId,
        [Description("The test result ID.")] int testResultId,
        [Description("Attachment file name.")] string fileName,
        [Description("Base64-encoded file content.")] string base64Content,
        [Description("Optional comment.")] string comment = ""
    )
    {
        var client = await _adoService.GetTestManagementApiAsync();
        var project = _adoService.DefaultProject;

        var attachment = new TestAttachmentRequestModel(
            stream: base64Content,
            fileName: fileName,
            comment: string.IsNullOrEmpty(comment) ? null : comment
        );

#pragma warning disable CS0612 // Type or member is obsolete
        return await client.CreateTestResultAttachmentAsync(
            attachment,
            project,
            runId,
            testResultId
        );
#pragma warning restore CS0612
    }
}