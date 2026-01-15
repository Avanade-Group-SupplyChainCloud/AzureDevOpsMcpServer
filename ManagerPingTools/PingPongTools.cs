using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.ManagerPingTools;

[McpServerToolType]
public class PingPongTools
{
    [McpServerTool(Name = "ping")]
    [Description("Health check tool. Returns 'pong'.")]
    public string Ping(
        [Description("Optional payload to echo back.")] string message = ""
    )
    {
        return string.IsNullOrEmpty(message) ? "pong" : $"pong: {message}";
    }
}
