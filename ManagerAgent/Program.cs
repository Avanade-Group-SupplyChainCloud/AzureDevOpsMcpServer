using AzureDevOpsMcp.Manager.Tools;
using AzureDevOpsMcp.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureDevOpsMcp(typeof(WorkItemTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.Run();