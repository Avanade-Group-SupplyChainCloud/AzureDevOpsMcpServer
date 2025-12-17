using AzureDevOpsMcp.Shared.Services;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureDevOpsSettings>(builder.Configuration.GetSection("AzureDevOps"));
builder.Services.AddSingleton<AzureDevOpsService>();

builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();

app.Use(async (context, next) =>
{
    var apiKey = app.Configuration["ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        if (!context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey) || !string.Equals(extractedApiKey, apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }
    }
    await next();
});

app.MapMcp();

app.Run();