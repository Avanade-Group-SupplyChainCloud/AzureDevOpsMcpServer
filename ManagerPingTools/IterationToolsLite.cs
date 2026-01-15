using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.Work.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.ManagerPingTools;

[McpServerToolType]
public class IterationToolsLite(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_iterations")]
    [Description("List all iterations (sprints) for a team.")]
    public async Task<IEnumerable<TeamSettingsIteration>> ListIterations(
        [Description("The name or ID of the team.")] string team,
        [Description("Filter by timeframe: 'current', 'past', or 'future'.")] string timeframe = ""
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        var iterations = await client.GetTeamIterationsAsync(
            teamContext,
            string.IsNullOrEmpty(timeframe) ? null : timeframe
        );
        return iterations ?? Enumerable.Empty<TeamSettingsIteration>();
    }

    [McpServerTool(Name = "get_current_iteration")]
    [Description("Get the current iteration (sprint) for a team.")]
    public async Task<TeamSettingsIteration> GetCurrentIteration(
        [Description("The name or ID of the team.")] string team
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        var iterations = await client.GetTeamIterationsAsync(teamContext, "current");
        return iterations?.FirstOrDefault();
    }

    [McpServerTool(Name = "get_team_capacity")]
    [Description("Get the capacity settings for a team in a specific iteration.")]
    public async Task<IEnumerable<TeamMemberCapacityIdentityRef>> GetTeamCapacity(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration (GUID).")]
            string iterationId
    )
    {
        if (!Guid.TryParse(iterationId, out var iterationGuid))
            throw new ArgumentException("iterationId must be a GUID.");

        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        var capacities = await client.GetCapacitiesWithIdentityRefAsync(teamContext, iterationGuid);
        return capacities ?? Enumerable.Empty<TeamMemberCapacityIdentityRef>();
    }

    [McpServerTool(Name = "get_team_days_off")]
    [Description("Get the team days off for a specific iteration.")]
    public async Task<TeamSettingsDaysOff> GetTeamDaysOff(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration (GUID).")]
            string iterationId
    )
    {
        if (!Guid.TryParse(iterationId, out var iterationGuid))
            throw new ArgumentException("iterationId must be a GUID.");

        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        return await client.GetTeamDaysOffAsync(teamContext, iterationGuid);
    }

    [McpServerTool(Name = "get_team_settings")]
    [Description("Get the team settings including backlog iteration, default iteration, and working days.")]
    public async Task<TeamSetting> GetTeamSettings(
        [Description("The name or ID of the team.")] string team
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        return await client.GetTeamSettingsAsync(teamContext);
    }

    [McpServerTool(Name = "get_iteration_capacities")]
    [Description("Get an iteration's capacity for all teams.")]
    public async Task<string> GetIterationCapacities(
        [Description("The ID of the iteration.")] string iterationId
    )
    {
        var connection = _adoService.Connection;
        var project = _adoService.DefaultProject;
        var baseUrl = connection.Uri.ToString().TrimEnd('/');
        var url =
            $"{baseUrl}/{project}/_apis/work/iterations/{Uri.EscapeDataString(iterationId)}/iterationcapacities?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error getting iteration capacities: {response.StatusCode} - {content}";

        return content;
    }
}
