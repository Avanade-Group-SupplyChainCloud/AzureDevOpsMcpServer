namespace AzureDevOpsMcp.Shared.Models
{
    public class McpToolResult
    {
        public List<McpContent> Content { get; set; } = new();
        public bool IsError { get; set; }
    }

    public class McpContent
    {
        public string Type { get; set; } = "text";
        public string Text { get; set; }
    }
}
