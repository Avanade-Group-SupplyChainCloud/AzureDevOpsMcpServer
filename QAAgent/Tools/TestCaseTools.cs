using System.ComponentModel;
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
}
