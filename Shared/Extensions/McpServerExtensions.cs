using System.Reflection;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var appInsightsConnStr =
            builder.Configuration["ApplicationInsights:ConnectionString"]
            ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnStr))
        {
            builder.Services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = appInsightsConnStr;
            });
        }

        // Configure Azure DevOps settings
        builder
            .Services.AddOptions<AzureDevOpsSettings>()
            .Bind(builder.Configuration.GetSection("AzureDevOps"))
            .Validate(s => !string.IsNullOrWhiteSpace(s.OrgUrl), "AzureDevOps:OrgUrl is required.")
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.TenantId),
                "AzureDevOps:TenantId is required."
            )
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.ClientId),
                "AzureDevOps:ClientId is required."
            )
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.ClientSecret),
                "AzureDevOps:ClientSecret is required."
            )
            .ValidateOnStart();
        builder.Services.AddSingleton<AzureDevOpsService>();

        // Add MCP Server and register tools
        builder
            .Services.AddMcpServer()
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
        // app.UseHttpsRedirection();
        app.UseCors();

        // Health endpoint – outside MCP pipeline and auth
        app.Map(
            "/health",
            healthApp =>
            {
                healthApp.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    return Task.CompletedTask;
                });
            }
        );

        // MCP Streamable HTTP returns text/event-stream responses. Some clients send
        // Accept: application/json which triggers 406. Normalize it to avoid the issue.
        // this was required for Avanade AI

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

        static async Task<bool> TryAuthorizeRequestAsync(HttpContext context, string apiKey)
        {
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

            if (authorized)
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return false;
        }

        app.Use(
            async (context, next) =>
            {
                if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/")
                {
                    var accept = context.Request.Headers.Accept.ToString();
                    if (
                        !accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
                        && !accept.Contains("*/*", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        context.Request.Headers.Accept = "text/event-stream, */*";
                    }
                }

                // Auth (optional): ApiKey can be supplied via x-api-key or Authorization (Bearer or raw)
                var apiKey = app.Configuration["ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var isAuthorized = await TryAuthorizeRequestAsync(context, apiKey);
                    if (!isAuthorized)
                    {
                        return;
                    }
                }

                await next();
            }
        );

        app.MapMcp();

        return app;
    }
}
