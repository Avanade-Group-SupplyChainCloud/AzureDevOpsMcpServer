using System.ComponentModel;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Core.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class CoreTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_projects")]
    [Description(
        "List all projects in the Azure DevOps organization. Supports filtering by state and name."
    )]
    public async Task<IEnumerable<TeamProjectReference>> ListProjects(
        [Description("Filter projects by their state. Defaults to 'wellFormed'.")]
            string stateFilter = "wellFormed",
        [Description("The maximum number of projects to return. Defaults to 100.")] int top = 100,
        [Description("The number of projects to skip for pagination. Defaults to 0.")] int skip = 0,
        [Description("Filter projects by name. Supports partial matches.")]
            string projectNameFilter = null
    )
    {
        var client = await _adoService.GetCoreApiAsync();
        ProjectState state = stateFilter switch
        {
            "deleted" => ProjectState.Deleted,
            "createPending" => ProjectState.CreatePending,
            "wellFormed" => ProjectState.WellFormed,
            "all" => ProjectState.All,
            _ => ProjectState.WellFormed,
        };

        var projects = await client.GetProjects(state, top, skip);

        if (projects == null)
        {
            return Enumerable.Empty<TeamProjectReference>();
        }

        IEnumerable<TeamProjectReference> filteredProjects = projects;
        if (!string.IsNullOrEmpty(projectNameFilter))
        {
            filteredProjects = projects.Where(p =>
                p.Name.Contains(projectNameFilter, StringComparison.OrdinalIgnoreCase)
            );
        }

        return filteredProjects;
    }

    [McpServerTool(Name = "list_teams")]
    [Description("List all teams in a specific Azure DevOps project.")]
    public async Task<IEnumerable<WebApiTeam>> ListProjectTeams(
        [Description("The name or ID of the Azure DevOps project.")] string project,
        [Description("If true, only return teams that the authenticated user is a member of.")]
            bool mine = false,
        [Description("The maximum number of teams to return. Defaults to 100.")] int top = 100,
        [Description("The number of teams to skip for pagination. Defaults to 0.")] int skip = 0
    )
    {
        var teamClient = await _adoService.GetTeamApiAsync();
        var teams = await teamClient.GetTeamsAsync(project, mine, top, skip);

        return teams ?? Enumerable.Empty<WebApiTeam>();
    }
}