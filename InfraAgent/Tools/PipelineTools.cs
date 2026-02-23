using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Infra.Tools;

[McpServerToolType]
public class PipelineTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_builds")]
    [Description("List builds, optionally filtered by pipeline definition.")]
    public async Task<IEnumerable<Build>> GetBuilds(
        [Description("A list of build definition IDs to filter by.")]
            IEnumerable<int> definitions = null,
        [Description("The maximum number of builds to return. Defaults to 100.")] int top = 100
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        var builds = await client.GetBuildsAsync(project, definitions: definitions, top: top);
        return builds ?? Enumerable.Empty<Build>();
    }

    [McpServerTool(Name = "list_pipelines")]
    [Description("List all pipeline/build definitions.")]
    public async Task<IEnumerable<BuildDefinitionReference>> GetBuildDefinitions(
        [Description("Filter by definition name.")] string name = null
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        var definitions = await client.GetDefinitionsAsync(project, name: name);
        return definitions ?? Enumerable.Empty<BuildDefinitionReference>();
    }

    [McpServerTool(Name = "get_run")]
    [Description("Get details of a specific pipeline run.")]
    public async Task<Build> GetRun(
        [Description("The ID of the pipeline.")] int pipelineId,
        [Description("The ID of the run.")] int runId
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetBuildAsync(project, runId);
    }

    [McpServerTool(Name = "list_runs")]
    [Description("List runs for a specific pipeline.")]
    public async Task<IEnumerable<Build>> ListRuns(
        [Description("The ID of the pipeline.")] int pipelineId
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        var builds = await client.GetBuildsAsync(project, definitions: new[] { pipelineId });
        return builds ?? Enumerable.Empty<Build>();
    }

    [McpServerTool(Name = "run_pipeline")]
    [Description("Queue a new build/pipeline run for a definition.")]
    public async Task<Build> RunPipeline(
        [Description("The ID of the build definition to run.")] int definitionId,
        [Description("The branch to build. If not specified, the default branch is used.")]
            string branchName = null
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        var build = new Build
        {
            Definition = new BuildDefinitionReference { Id = definitionId },
            SourceBranch = branchName,
        };
        return await client.QueueBuildAsync(build, project);
    }

    [McpServerTool(Name = "get_build_status")]
    [Description("Get the status of a specific build.")]
    public async Task<Build> GetBuildStatus(
        [Description("The build ID.")] int buildId
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetBuildAsync(project, buildId);
    }

    [McpServerTool(Name = "get_build_definition_revisions")]
    [Description("Get revision history of a build definition.")]
    public async Task<IEnumerable<BuildDefinitionRevision>> GetBuildDefinitionRevisions(
        [Description("The build definition ID.")] int definitionId
    )
    {
        var client = await _adoService.GetBuildApiAsync();
        var project = _adoService.DefaultProject;
        var revisions = await client.GetDefinitionRevisionsAsync(project, definitionId);
        return revisions ?? Enumerable.Empty<BuildDefinitionRevision>();
    }

    [McpServerTool(Name = "update_build_stage")]
    [Description(
        "Update a build stage (cancel, retry, or run). Useful for stage-level control in YAML pipelines."
    )]
    public async Task<string> UpdateBuildStage(
        [Description("The build ID.")] int buildId,
        [Description("The name of the stage to update.")] string stageName,
        [Description("The status to set: 'cancel' or 'retry'.")] string status,
        [Description("Force retry all jobs in the stage.")] bool forceRetryAllJobs = false
    )
    {
        var connection = _adoService.Connection;
        var project = _adoService.DefaultProject;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url =
            $"{baseUrl}/{project}/_apis/build/builds/{buildId}/stages/{stageName}?api-version=7.1-preview.1";

        var body = new
        {
            forceRetryAllJobs = forceRetryAllJobs,
            state = status.ToLower() switch
            {
                "cancel" => "cancel",
                "retry" => "retry",
                _ => status,
            },
        };

        using var httpClient = _adoService.CreateHttpClient();

        var response = await httpClient.PatchAsJsonAsync(url, body);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error updating build stage: {response.StatusCode} - {content}";

        return content;
    }
}