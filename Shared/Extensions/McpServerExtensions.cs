using System.Reflection;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AzureDevOpsMcp.Shared.Extensions;

public static class McpServerExtensions
{
    public static WebApplicationBuilder AddAzureDevOpsMcp(
        this WebApplicationBuilder builder,
        Assembly toolsAssembly
    )
    {
        builder.Services.AddApplicationInsightsTelemetry();

        // Configure Azure DevOps settings
        builder.Services.Configure<AzureDevOpsSettings>(
            builder.Configuration.GetSection("AzureDevOps")
        );
        builder.Services.AddSingleton<AzureDevOpsService>();

        // Add MCP Server and register tools
        builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly(toolsAssembly);

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        return builder;
    }

    public static WebApplication UseAzureDevOpsMcp(this WebApplication app)
    {
        // Global exception handler - catches all exceptions in the request pipeline
        app.Use(
            async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            }
        );

        // app.UseHttpsRedirection();
        app.UseCors();

        // API Key Middleware
        app.Use(
            async (context, next) =>
            {
                var apiKey = app.Configuration["ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    if (
                        !context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey)
                        || !string.Equals(extractedApiKey, apiKey)
                    )
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized");
                        return;
                    }
                }
                await next();
            }
        );

        app.MapMcp();
        app.MapGet("/health", () => Results.Ok("ok"));

        return app;
    }
}