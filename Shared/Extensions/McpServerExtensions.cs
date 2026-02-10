using System.Reflection;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

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
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(toolsAssembly)
            .WithListPromptsHandler((_, _) => ValueTask.FromResult(new ListPromptsResult()))
            .WithListResourcesHandler((_, _) => ValueTask.FromResult(new ListResourcesResult()));

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
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(
            "AzureDevOpsMcp.RequestHeaders"
        );

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
                    logger.LogError(
                        ex,
                        "Unhandled exception in {Method} {Path}: {Message}",
                        context.Request.Method,
                        context.Request.Path,
                        ex.Message
                    );
                    throw;
                }
            }
        );

        // app.UseHttpsRedirection();
        app.UseCors();

        // Simple header logging for diagnostics
        app.Use(
            async (context, next) =>
            {
                if (app.Configuration.GetValue("AppInsights:LogRequestHeaders", false))
                {
                    var headers = string.Join(
                        ";",
                        context.Request.Headers.Select(h => $"{h.Key}={h.Value}")
                    );
                    logger.LogInformation(
                        "{Method} {Path} Headers: {Headers}",
                        context.Request.Method,
                        context.Request.Path,
                        headers
                    );
                }

                await next();
            }
        );

        // MCP Streamable HTTP returns text/event-stream responses. Some clients send
        // Accept: application/json which triggers 406. Normalize it to avoid the issue.
        app.Use(
            async (context, next) =>
            {
                if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/")
                {
                    var accept = context.Request.Headers.Accept.ToString();
                    if (!accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
                        && !accept.Contains("*/*", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Request.Headers.Accept = "text/event-stream, */*";
                    }
                }

                await next();
            }
        );

        // Auth Middleware (optional): ApiKey can be supplied via x-api-key or Authorization (Bearer or raw)
        app.Use(
            async (context, next) =>
            {
                var apiKey = app.Configuration["ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    await next();
                    return;
                }

                static string ExtractAuthorizationToken(string authHeader)
                {
                    if (string.IsNullOrWhiteSpace(authHeader))
                    {
                        return string.Empty;
                    }

                    const string bearerPrefix = "Bearer ";
                    return authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                        ? authHeader[bearerPrefix.Length..].Trim()
                        : authHeader.Trim();
                }

                static bool TokenMatchesApiKey(string token, string configuredApiKey)
                {
                    if (Guid.TryParse(configuredApiKey, out var expectedGuid))
                    {
                        return Guid.TryParse(token, out var providedGuid) && providedGuid == expectedGuid;
                    }

                    return string.Equals(token, configuredApiKey, StringComparison.Ordinal);
                }

                var tokenFromApiKeyHeader = context.Request.Headers.TryGetValue(
                    "x-api-key",
                    out var extractedApiKey
                )
                    ? extractedApiKey.ToString().Trim()
                    : string.Empty;

                var tokenFromAuthorizationHeader = ExtractAuthorizationToken(
                    context.Request.Headers.Authorization.ToString()
                );

                var authorized =
                    TokenMatchesApiKey(tokenFromApiKeyHeader, apiKey)
                    || TokenMatchesApiKey(tokenFromAuthorizationHeader, apiKey);

                if (!authorized)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }

                await next();
            }
        );

        app.MapMcp();
        app.MapGet("/health", () => Results.Ok("ok"));

        return app;
    }
}