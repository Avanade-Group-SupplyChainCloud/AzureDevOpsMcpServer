using AzureDevOpsMcp.Manager.Tools;
using AzureDevOpsMcp.Shared.Extensions;
using AzureDevOpsMcp.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureDevOpsMcp(typeof(WorkItemTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.MapGet("/deep-dive/{workItemId:int}", async (int workItemId, AzureDevOpsService adoService, HttpContext httpContext) =>
{
    var apiKey = app.Configuration["ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        var provided = httpContext.Request.Query.TryGetValue("apiKey", out var q) ? q.ToString().Trim() : string.Empty;
        var valid = Guid.TryParse(apiKey, out var expected)
            ? Guid.TryParse(provided, out var providedGuid) && providedGuid == expected
            : string.Equals(provided, apiKey, StringComparison.Ordinal);

        if (!valid)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsync("Unauthorized");
            return;
        }
    }

    var tool = new WorkItemDeepDiveTools(adoService);
    var result = await tool.WorkItemDeepDive(workItemId);
    httpContext.Response.ContentType = "application/json";
    await httpContext.Response.WriteAsync(result);
});

app.Run();