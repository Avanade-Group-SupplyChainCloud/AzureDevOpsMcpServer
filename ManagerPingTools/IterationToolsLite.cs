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
}
