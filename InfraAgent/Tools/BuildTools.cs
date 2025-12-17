using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AzureDevOpsMcp.Infra.Tools
{
    [McpServerToolType]
    public class BuildTools
    {
        private readonly AzureDevOpsService _adoService;

        public BuildTools(AzureDevOpsService adoService)
        {
            _adoService = adoService;
        }

        [McpServerTool(Name = "get_build")]
        [Description("Get detailed information about a specific build.")]
        public async Task<Build> GetBuild(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId)
        {
            var client = await _adoService.GetBuildApiAsync();
            return await client.GetBuildAsync(project, buildId);
        }

        [McpServerTool(Name = "get_build_logs")]
        [Description("Get the log entries for a build.")]
        public async Task<IEnumerable<BuildLog>> GetBuildLogs(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId)
        {
            var client = await _adoService.GetBuildApiAsync();
            var logs = await client.GetBuildLogsAsync(project, buildId);
            return logs ?? Enumerable.Empty<BuildLog>();
        }

        [McpServerTool(Name = "get_build_log_content")]
        [Description("Get the content of a specific build log.")]
        public async Task<string> GetBuildLogContent(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId,
            [Description("The ID of the log to retrieve.")] int logId)
        {
            var client = await _adoService.GetBuildApiAsync();
            var logStream = await client.GetBuildLogAsync(project, buildId, logId);
            using var reader = new StreamReader(logStream);
            return await reader.ReadToEndAsync();
        }

        [McpServerTool(Name = "get_build_timeline")]
        [Description("Get the timeline (stages, jobs, tasks) of a build.")]
        public async Task<Timeline> GetBuildTimeline(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId)
        {
            var client = await _adoService.GetBuildApiAsync();
            return await client.GetBuildTimelineAsync(project, buildId);
        }

        [McpServerTool(Name = "get_build_changes")]
        [Description("Get the source changes associated with a build.")]
        public async Task<IEnumerable<Change>> GetBuildChanges(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId,
            [Description("Maximum number of changes to return.")] int top = 50)
        {
            var client = await _adoService.GetBuildApiAsync();
            var changes = await client.GetBuildChangesAsync(project, buildId, top: top);
            return changes ?? Enumerable.Empty<Change>();
        }

        [McpServerTool(Name = "get_build_work_items")]
        [Description("Get work items associated with a build.")]
        public async Task<IEnumerable<ResourceRef>> GetBuildWorkItems(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId,
            [Description("Maximum number of work items to return.")] int top = 50)
        {
            var client = await _adoService.GetBuildApiAsync();
            var workItems = await client.GetBuildWorkItemsRefsAsync(project, buildId, top: top);
            return workItems ?? Enumerable.Empty<ResourceRef>();
        }

        [McpServerTool(Name = "cancel_build")]
        [Description("Cancel a running build.")]
        public async Task<Build> CancelBuild(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build to cancel.")] int buildId)
        {
            var client = await _adoService.GetBuildApiAsync();
            var build = new Build { Status = BuildStatus.Cancelling };
            return await client.UpdateBuildAsync(build, project, buildId);
        }

        [McpServerTool(Name = "get_build_artifacts")]
        [Description("Get the artifacts published by a build.")]
        public async Task<IEnumerable<BuildArtifact>> GetBuildArtifacts(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the build.")] int buildId)
        {
            var client = await _adoService.GetBuildApiAsync();
            var artifacts = await client.GetArtifactsAsync(project, buildId);
            return artifacts ?? Enumerable.Empty<BuildArtifact>();
        }

        [McpServerTool(Name = "get_pipeline_definition")]
        [Description("Get detailed information about a pipeline/build definition.")]
        public async Task<BuildDefinition> GetPipelineDefinition(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the pipeline definition.")] int definitionId)
        {
            var client = await _adoService.GetBuildApiAsync();
            return await client.GetDefinitionAsync(project, definitionId);
        }

        [McpServerTool(Name = "get_pipeline_runs")]
        [Description("Get recent runs of a specific pipeline.")]
        public async Task<IEnumerable<Build>> GetPipelineRuns(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the pipeline definition.")] int definitionId,
            [Description("Maximum number of builds to return.")] int top = 20,
            [Description("Filter by status: 'inProgress', 'completed', 'cancelling', 'postponed', 'notStarted', 'all'.")] string statusFilter = "",
            [Description("Filter by result: 'succeeded', 'partiallySucceeded', 'failed', 'canceled', 'none'.")] string resultFilter = "")
        {
            var client = await _adoService.GetBuildApiAsync();

            BuildStatus? status = string.IsNullOrEmpty(statusFilter) ? null : statusFilter.ToLower() switch
            {
                "inprogress" => BuildStatus.InProgress,
                "completed" => BuildStatus.Completed,
                "cancelling" => BuildStatus.Cancelling,
                "postponed" => BuildStatus.Postponed,
                "notstarted" => BuildStatus.NotStarted,
                "all" => BuildStatus.All,
                _ => null
            };

            BuildResult? result = string.IsNullOrEmpty(resultFilter) ? null : resultFilter.ToLower() switch
            {
                "succeeded" => BuildResult.Succeeded,
                "partiallysucceeded" => BuildResult.PartiallySucceeded,
                "failed" => BuildResult.Failed,
                "canceled" => BuildResult.Canceled,
                "none" => BuildResult.None,
                _ => null
            };

            var builds = await client.GetBuildsAsync(project, definitions: new[] { definitionId }, statusFilter: status, resultFilter: result, top: top);
            return builds ?? Enumerable.Empty<Build>();
        }

        [McpServerTool(Name = "queue_build_with_parameters")]
        [Description("Queue a new build with custom parameters and variables.")]
        public async Task<Build> QueueBuildWithParameters(
            [Description("The project name or ID.")] string project,
            [Description("The ID of the pipeline definition.")] int definitionId,
            [Description("The branch to build (e.g., 'refs/heads/main').")] string sourceBranch = "",
            [Description("Reason for the build: 'manual', 'schedule', etc.")] string reason = "manual")
        {
            var client = await _adoService.GetBuildApiAsync();

            var build = new Build
            {
                Definition = new BuildDefinitionReference { Id = definitionId },
                Reason = reason.ToLower() switch
                {
                    "schedule" => BuildReason.Schedule,
                    "pullrequest" => BuildReason.PullRequest,
                    "individualci" => BuildReason.IndividualCI,
                    "batchedci" => BuildReason.BatchedCI,
                    _ => BuildReason.Manual
                }
            };

            if (!string.IsNullOrEmpty(sourceBranch))
            {
                build.SourceBranch = sourceBranch;
            }

            return await client.QueueBuildAsync(build, project);
        }
    }
}
