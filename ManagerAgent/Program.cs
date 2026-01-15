using AzureDevOpsMcp.ManagerPingTools;
using AzureDevOpsMcp.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights for Azure logging (uses APPLICATIONINSIGHTS_CONNECTION_STRING env var)
builder.Services.AddApplicationInsightsTelemetry();

// Set minimum log level for MCP logging
builder.Logging.AddFilter("MCP.RequestResponse", LogLevel.Information);

builder.AddAzureDevOpsMcp(typeof(PingPongTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.Run();