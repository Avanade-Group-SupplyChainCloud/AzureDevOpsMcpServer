using AzureDevOpsMcpServer.Services;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.Core.WebApi.Types;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace AzureDevOpsMcpServer.Tools
{
    [McpServerToolType]
    public class IterationTools
    {
        private readonly AzureDevOpsService _adoService;

        public IterationTools(AzureDevOpsService adoService)
        {
            _adoService = adoService;
        }

        [McpServerTool(Name = "list_iterations")]
        [Description("List all iterations (sprints) for a team in a project.")]
        public async Task<IEnumerable<TeamSettingsIteration>> ListIterations(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("Filter by timeframe: 'current', 'past', or 'future'.")] string timeframe = "")
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            var iterations = await client.GetTeamIterationsAsync(teamContext, string.IsNullOrEmpty(timeframe) ? null : timeframe);
            return iterations ?? Enumerable.Empty<TeamSettingsIteration>();
        }

        [McpServerTool(Name = "get_current_iteration")]
        [Description("Get the current iteration (sprint) for a team.")]
        public async Task<TeamSettingsIteration> GetCurrentIteration(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            var iterations = await client.GetTeamIterationsAsync(teamContext, "current");
            return iterations?.FirstOrDefault();
        }

        [McpServerTool(Name = "get_iteration_work_items")]
        [Description("Get all work items assigned to a specific iteration.")]
        public async Task<IterationWorkItems> GetIterationWorkItems(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            return await client.GetIterationWorkItemsAsync(teamContext, iterationId);
        }

        [McpServerTool(Name = "create_iteration")]
        [Description("Create a new iteration (sprint) in the project's classification nodes.")]
        public async Task<WorkItemClassificationNode> CreateIteration(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name of the new iteration.")] string iterationName,
            [Description("The start date of the iteration (ISO format).")] DateTime? startDate = null,
            [Description("The finish date of the iteration (ISO format).")] DateTime? finishDate = null,
            [Description("The path to the parent iteration (optional).")] string? parentPath = null)
        {
            var witClient = await _adoService.GetWorkItemTrackingApiAsync();
            
            var newIteration = new WorkItemClassificationNode
            {
                Name = iterationName,
                StructureType = TreeNodeStructureType.Iteration
            };

            if (startDate.HasValue || finishDate.HasValue)
            {
                newIteration.Attributes = new Dictionary<string, object>();
                if (startDate.HasValue)
                    newIteration.Attributes["startDate"] = startDate.Value.ToString("o");
                if (finishDate.HasValue)
                    newIteration.Attributes["finishDate"] = finishDate.Value.ToString("o");
            }

            return await witClient.CreateOrUpdateClassificationNodeAsync(
                newIteration,
                project,
                TreeStructureGroup.Iterations,
                parentPath);
        }

        [McpServerTool(Name = "assign_iteration_to_team")]
        [Description("Assign an existing iteration to a team.")]
        public async Task<TeamSettingsIteration> AssignIterationToTeam(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration to assign.")] Guid iterationId)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            var iterationToAdd = new TeamSettingsIteration { Id = iterationId };
            return await client.PostTeamIterationAsync(iterationToAdd, teamContext);
        }

        [McpServerTool(Name = "remove_iteration_from_team")]
        [Description("Remove an iteration from a team's assigned iterations.")]
        public async Task<string> RemoveIterationFromTeam(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration to remove.")] Guid iterationId)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            await client.DeleteTeamIterationAsync(teamContext, iterationId);
            return $"Iteration {iterationId} removed from team {team}";
        }

        [McpServerTool(Name = "get_iteration_capacities")]
        [Description("Get an iteration's capacity for all teams in iteration and project.")]
        public async Task<string> GetIterationCapacities(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The ID of the iteration.")] string iterationId)
        {
            var connection = _adoService.Connection;
            var baseUrl = connection.Uri.ToString().TrimEnd('/');
            var url = $"{baseUrl}/{project}/_apis/work/iterations/{iterationId}/iterationcapacities?api-version=7.1";

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
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            var capacities = await client.GetCapacitiesWithIdentityRefAsync(teamContext, iterationId);
            return capacities ?? Enumerable.Empty<TeamMemberCapacityIdentityRef>();
        }

        [McpServerTool(Name = "update_team_member_capacity")]
        [Description("Update capacity for a team member in an iteration.")]
        public async Task<TeamMemberCapacityIdentityRef> UpdateTeamMemberCapacity(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId,
            [Description("The ID of the team member.")] Guid teamMemberId,
            [Description("Daily capacity in hours.")] double capacityPerDay,
            [Description("Activity name (e.g., 'Development', 'Testing').")] string activity = "Development")
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            var capacityPatch = new CapacityPatch
            {
                Activities = new List<Activity>
                {
                    new Activity
                    {
                        Name = activity,
                        CapacityPerDay = (float)capacityPerDay
                    }
                }
            };
            
            return await client.UpdateCapacityWithIdentityRefAsync(capacityPatch, teamContext, iterationId, teamMemberId);
        }

        [McpServerTool(Name = "replace_team_capacities")]
        [Description("Replace all team member capacities for an iteration.")]
        public async Task<string> ReplaceTeamCapacities(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId,
            [Description("JSON array of capacity updates [{teamMemberId, capacityPerDay, activity}].")] string capacitiesJson)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            var capacityList = JsonSerializer.Deserialize<List<CapacityInput>>(capacitiesJson) ?? new List<CapacityInput>();
            var results = new List<object>();
            
            foreach (var input in capacityList)
            {
                var capacityPatch = new CapacityPatch
                {
                    Activities = new List<Activity>
                    {
                        new Activity
                        {
                            Name = input.Activity ?? "Development",
                            CapacityPerDay = (float)input.CapacityPerDay
                        }
                    }
                };
                
                var result = await client.UpdateCapacityWithIdentityRefAsync(capacityPatch, teamContext, iterationId, input.TeamMemberId);
                results.Add(result);
            }
            
            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "get_team_days_off")]
        [Description("Get the team days off for a specific iteration.")]
        public async Task<TeamSettingsDaysOff> GetTeamDaysOff(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            return await client.GetTeamDaysOffAsync(teamContext, iterationId);
        }

        [McpServerTool(Name = "set_team_days_off")]
        [Description("Set days off for the team in a specific iteration.")]
        public async Task<TeamSettingsDaysOff> SetTeamDaysOff(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The ID of the iteration.")] Guid iterationId,
            [Description("JSON array of date ranges [{start, end}] for days off.")] string daysOffJson)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            var dateRanges = JsonSerializer.Deserialize<List<DateRangeInput>>(daysOffJson) ?? new List<DateRangeInput>();
            
            var daysOffPatch = new TeamSettingsDaysOffPatch
            {
                DaysOff = dateRanges.Select(d => new DateRange
                {
                    Start = d.Start,
                    End = d.End
                }).ToList()
            };
            
            return await client.UpdateTeamDaysOffAsync(daysOffPatch, teamContext, iterationId);
        }

        [McpServerTool(Name = "get_team_settings")]
        [Description("Get the team settings including backlog iteration, default iteration, and working days.")]
        public async Task<TeamSetting> GetTeamSettings(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            return await client.GetTeamSettingsAsync(teamContext);
        }

        [McpServerTool(Name = "update_team_settings")]
        [Description("Update team settings like default iteration, backlog iteration, and working days.")]
        public async Task<TeamSetting> UpdateTeamSettings(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The default iteration ID (GUID).")] Guid? defaultIterationId = null,
            [Description("The backlog iteration ID (GUID).")] Guid? backlogIterationId = null,
            [Description("Working days as JSON array (e.g., ['monday','tuesday','wednesday','thursday','friday']).")] string? workingDaysJson = null)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            
            var patch = new TeamSettingsPatch();
            
            if (defaultIterationId.HasValue)
                patch.DefaultIteration = defaultIterationId.Value;
            
            if (backlogIterationId.HasValue)
                patch.BacklogIteration = backlogIterationId.Value;
            
            if (!string.IsNullOrEmpty(workingDaysJson))
            {
                var days = JsonSerializer.Deserialize<List<string>>(workingDaysJson) ?? new List<string>();
                patch.WorkingDays = days.Select(d => Enum.Parse<DayOfWeek>(d, true)).ToArray();
            }
            
            return await client.UpdateTeamSettingsAsync(patch, teamContext);
        }

        [McpServerTool(Name = "get_board_columns")]
        [Description("Get the columns configured for a team's board.")]
        public async Task<IEnumerable<BoardColumn>> GetBoardColumns(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team,
            [Description("The name of the board (e.g., 'Stories', 'Bugs').")] string board)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            var boardColumns = await client.GetBoardColumnsAsync(teamContext, board);
            return boardColumns ?? Enumerable.Empty<BoardColumn>();
        }

        [McpServerTool(Name = "get_boards")]
        [Description("Get all boards for a team.")]
        public async Task<IEnumerable<BoardReference>> GetBoards(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The name or ID of the team.")] string team)
        {
            var client = await _adoService.GetWorkApiAsync();
            var teamContext = new TeamContext(project, team);
            var boards = await client.GetBoardsAsync(teamContext);
            return boards ?? Enumerable.Empty<BoardReference>();
        }

        // Helper classes for JSON deserialization
        private class CapacityInput
        {
            public Guid TeamMemberId { get; set; }
            public double CapacityPerDay { get; set; }
            public string? Activity { get; set; }
        }

        private class DateRangeInput
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }
    }
}
