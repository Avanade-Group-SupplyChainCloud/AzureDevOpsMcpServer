using AzureDevOpsMcp.Infra.Tools;
using AzureDevOpsMcp.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureDevOpsMcp(typeof(BuildTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.Run();
