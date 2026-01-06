using AzureDevOpsMcp.QA.Tools;
using AzureDevOpsMcp.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureDevOpsMcp(typeof(TestPlanTools).Assembly);

var app = builder.Build();

app.UseAzureDevOpsMcp();

app.Run();