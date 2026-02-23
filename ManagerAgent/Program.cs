using AzureDevOpsMcp.Manager.Tools;
using AzureDevOpsMcp.Shared.Extensions;
using AzureDevOpsMcp.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureDevOpsMcp(typeof(WorkItemTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.MapGet("/deep-dive/{workItemId:int}", async (int workItemId, AzureDevOpsService adoService) =>
{
    var tool = new WorkItemDeepDiveTools(adoService);
    var result = await tool.WorkItemDeepDive(workItemId);
    return Results.Content(result, "application/json");
});

app.Run();