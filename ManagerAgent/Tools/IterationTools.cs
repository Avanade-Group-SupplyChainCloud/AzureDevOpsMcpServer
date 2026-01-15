using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.Work.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class IterationTools(AzureDevOpsService adoService)
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
            $"{baseUrl}/{project}/_apis/work/iterations/{iterationId}/iterationcapacities?api-version=7.1";

        using var httpClient = _adoService.CreateHttpClient();
        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Error getting iteration capacities: {response.StatusCode} - {content}";

        return content;
    }

    [McpServerTool(Name = "get_team_capacity")]
    [Description("Get the capacity settings for a team in a specific iteration.")]
    public async Task<IEnumerable<TeamMemberCapacityIdentityRef>> GetTeamCapacity(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration.")] Guid iterationId
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        var capacities = await client.GetCapacitiesWithIdentityRefAsync(teamContext, iterationId);
        return capacities ?? Enumerable.Empty<TeamMemberCapacityIdentityRef>();
    }

    [McpServerTool(Name = "update_team_member_capacity")]
    [Description("Update capacity for a team member in an iteration.")]
    public async Task<TeamMemberCapacityIdentityRef> UpdateTeamMemberCapacity(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration.")] Guid iterationId,
        [Description("The ID of the team member.")] Guid teamMemberId,
        [Description("Daily capacity in hours.")] double capacityPerDay,
        [Description("Activity name (e.g., 'Development', 'Testing').")]
            string activity = "Development"
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);

        var capacityPatch = new CapacityPatch
        {
            Activities = new List<Activity>
            {
                new Activity { Name = activity, CapacityPerDay = (float)capacityPerDay },
            },
        };

        return await client.UpdateCapacityWithIdentityRefAsync(
            capacityPatch,
            teamContext,
            iterationId,
            teamMemberId
        );
    }

    [McpServerTool(Name = "get_team_days_off")]
    [Description("Get the team days off for a specific iteration.")]
    public async Task<TeamSettingsDaysOff> GetTeamDaysOff(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration.")] Guid iterationId
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        return await client.GetTeamDaysOffAsync(teamContext, iterationId);
    }

    [McpServerTool(Name = "set_team_days_off")]
    [Description("Set days off for the team in a specific iteration.")]
    public async Task<TeamSettingsDaysOff> SetTeamDaysOff(
        [Description("The name or ID of the team.")] string team,
        [Description("The ID of the iteration.")] Guid iterationId,
        [Description("JSON array of date ranges [{start, end}] for days off.")] string daysOffJson
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);

        var dateRanges =
            JsonSerializer.Deserialize<List<DateRangeInput>>(daysOffJson)
            ?? new List<DateRangeInput>();

        var daysOffPatch = new TeamSettingsDaysOffPatch
        {
            DaysOff = dateRanges
                .Select(d => new DateRange { Start = d.Start, End = d.End })
                .ToList(),
        };

        return await client.UpdateTeamDaysOffAsync(daysOffPatch, teamContext, iterationId);
    }

    [McpServerTool(Name = "get_team_settings")]
    [Description(
        "Get the team settings including backlog iteration, default iteration, and working days."
    )]
    public async Task<TeamSetting> GetTeamSettings(
        [Description("The name or ID of the team.")] string team
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);
        return await client.GetTeamSettingsAsync(teamContext);
    }

    [McpServerTool(Name = "update_team_settings")]
    [Description(
        "Update team settings like default iteration, backlog iteration, and working days."
    )]
    public async Task<TeamSetting> UpdateTeamSettings(
        [Description("The name or ID of the team.")] string team,
        [Description("The default iteration ID (GUID). Use empty string to skip.")] string defaultIterationId = "",
        [Description("The backlog iteration ID (GUID). Use empty string to skip.")] string backlogIterationId = "",
        [Description(
            "Working days as JSON array (e.g., ['monday','tuesday','wednesday','thursday','friday']). Use empty string to skip."
        )]
            string workingDaysJson = ""
    )
    {
        var client = await _adoService.GetWorkApiAsync();
        var project = _adoService.DefaultProject;
        var teamContext = new TeamContext(project, team);

        var patch = new TeamSettingsPatch();

        if (!string.IsNullOrEmpty(defaultIterationId) && Guid.TryParse(defaultIterationId, out var defIterId))
            patch.DefaultIteration = defIterId;

        if (!string.IsNullOrEmpty(backlogIterationId) && Guid.TryParse(backlogIterationId, out var backIterId))
            patch.BacklogIteration = backIterId;

        if (!string.IsNullOrEmpty(workingDaysJson))
        {
            var days =
                JsonSerializer.Deserialize<List<string>>(workingDaysJson) ?? new List<string>();
            patch.WorkingDays = days.Select(d => Enum.Parse<DayOfWeek>(d, true)).ToArray();
        }

        return await client.UpdateTeamSettingsAsync(patch, teamContext);
    }

    // Helper classes for JSON deserialization
    private class CapacityInput
    {
        public Guid TeamMemberId { get; set; }
        public double CapacityPerDay { get; set; }
        public string Activity { get; set; }
    }

    private class DateRangeInput
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}