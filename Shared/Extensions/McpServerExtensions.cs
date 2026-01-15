using System.Reflection;
using System.Text;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureDevOpsMcp.Shared.Extensions;

public static class McpServerExtensions
{
    public static WebApplicationBuilder AddAzureDevOpsMcp(
        this WebApplicationBuilder builder,
        Assembly toolsAssembly
    )
    {
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
        // app.UseHttpsRedirection();
        app.UseCors();

        // MCP Request/Response Logging Middleware
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MCP.RequestResponse");

            // Only log MCP endpoint requests
            if (context.Request.Path.StartsWithSegments("/mcp"))
            {
                var method = context.Request.Method;
                var sessionId = context.Request.Headers["Mcp-Session-Id"].FirstOrDefault() ?? "(none)";
                
                logger.LogInformation("═══════════════════════════════════════════════════════════");
                logger.LogInformation("MCP Request: {Method} {Path}", method, context.Request.Path);
                logger.LogInformation("Session ID: {SessionId}", sessionId);
                logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType ?? "(none)");
                logger.LogInformation("Accept: {Accept}", context.Request.Headers.Accept.FirstOrDefault() ?? "(none)");

                // For POST requests, read and log the body
                if (method == "POST" && context.Request.ContentLength > 0)
                {
                    context.Request.EnableBuffering();
                    
                    using var reader = new StreamReader(
                        context.Request.Body,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        leaveOpen: true);
                    
                    var requestBody = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    // Try to parse and log the JSON-RPC method
                    try
                    {
                        using var doc = JsonDocument.Parse(requestBody);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("method", out var methodProp))
                        {
                            var rpcMethod = methodProp.GetString();
                            logger.LogInformation("JSON-RPC Method: {RpcMethod}", rpcMethod);
                            
                            if (root.TryGetProperty("id", out var idProp))
                            {
                                logger.LogInformation("JSON-RPC ID: {RpcId}", idProp.ToString());
                            }
                            
                            if (root.TryGetProperty("params", out var paramsProp))
                            {
                                logger.LogInformation("JSON-RPC Params: {Params}", paramsProp.ToString());
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        logger.LogWarning("Request body is not valid JSON: {Body}", 
                            requestBody.Length > 500 ? requestBody[..500] + "..." : requestBody);
                    }
                }

                // Capture the response
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                try
                {
                    await next();

                    // Log response info
                    logger.LogInformation("Response Status: {StatusCode}", context.Response.StatusCode);
                    logger.LogInformation("Response Content-Type: {ContentType}", 
                        context.Response.ContentType ?? "(none)");

                    // Read and log response body for non-SSE responses
                    if (context.Response.ContentType?.Contains("application/json") == true)
                    {
                        responseBody.Position = 0;
                        var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                        
                        // Try to parse and summarize the response
                        try
                        {
                            using var doc = JsonDocument.Parse(responseText);
                            var root = doc.RootElement;
                            
                            // Check if this is a tools/list response
                            if (root.TryGetProperty("result", out var result))
                            {
                                if (result.TryGetProperty("tools", out var tools) && 
                                    tools.ValueKind == JsonValueKind.Array)
                                {
                                    var toolCount = tools.GetArrayLength();
                                    logger.LogInformation("═══ TOOLS LIST RESPONSE ═══");
                                    logger.LogInformation("Total tools returned: {ToolCount}", toolCount);
                                    
                                    var toolNames = new List<string>();
                                    foreach (var tool in tools.EnumerateArray())
                                    {
                                        if (tool.TryGetProperty("name", out var nameProp))
                                        {
                                            var toolName = nameProp.GetString();
                                            toolNames.Add(toolName ?? "(unnamed)");
                                            
                                            // Log each tool's schema for debugging
                                            if (tool.TryGetProperty("inputSchema", out var schema))
                                            {
                                                logger.LogDebug("Tool '{ToolName}' schema: {Schema}", 
                                                    toolName, schema.ToString());
                                            }
                                        }
                                    }
                                    
                                    logger.LogInformation("Tools: {Tools}", string.Join(", ", toolNames));
                                }
                                else
                                {
                                    // Log other results (truncated)
                                    var resultStr = result.ToString();
                                    logger.LogInformation("Result: {Result}", 
                                        resultStr.Length > 1000 ? resultStr[..1000] + "..." : resultStr);
                                }
                            }
                            else if (root.TryGetProperty("error", out var error))
                            {
                                logger.LogWarning("JSON-RPC Error: {Error}", error.ToString());
                            }
                        }
                        catch (JsonException)
                        {
                            logger.LogDebug("Response (raw): {Response}", 
                                responseText.Length > 500 ? responseText[..500] + "..." : responseText);
                        }
                        
                        // Copy to original stream
                        responseBody.Position = 0;
                        await responseBody.CopyToAsync(originalBodyStream);
                    }
                    else
                    {
                        // For SSE or other content types, just copy through
                        responseBody.Position = 0;
                        await responseBody.CopyToAsync(originalBodyStream);
                        
                        if (context.Response.ContentType?.Contains("text/event-stream") == true)
                        {
                            logger.LogInformation("SSE stream started");
                        }
                    }
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }
                
                logger.LogInformation("═══════════════════════════════════════════════════════════");
            }
            else
            {
                await next();
            }
        });

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

        app.MapMcp("/mcp");

        return app;
    }
}