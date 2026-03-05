namespace AzureDevOpsMcp.Shared.Services;

public sealed class AzureAiSettings
{
    /// <summary>
    /// Azure AI Foundry / Azure OpenAI endpoint URL, e.g. https://{resource-name}.openai.azure.com/
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// API key for the Azure OpenAI resource.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Chat deployment name. (Azure uses deployment names, not raw model names.)
    /// </summary>
    public string Model { get; set; }
}
