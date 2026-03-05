using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AzureDevOpsMcp.Shared.Services;

public sealed class AiSummaryService
{
    private readonly AzureAiSettings _settings;
    private readonly ILogger<AiSummaryService> _logger;

    private readonly ChatClient _chatClient;

    public AiSummaryService(IOptions<AzureAiSettings> settings, ILogger<AiSummaryService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var endpoint = new Uri(_settings.Url);
        var client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(_settings.ApiKey));
        _chatClient = client.GetChatClient(_settings.Model);
    }

    public async Task<string> SummarizePrSnapshotAsync(string prSnapshotJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(prSnapshotJson);

            var completion = await _chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt),
                ],
                null,
                cancellationToken
            );

            var text = completion?.Value?.Content?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
                return "AI summary not generated (empty model response).";

            return text.Trim();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Azure AI summary call failed (Model={Model}).", _settings.Model);
            return $"AI summary not generated (Azure AI request failed: {ex.Status}).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AI summary call failed (Model={Model}).", _settings.Model);
            return "AI summary not generated (Azure AI request failed).";
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior engineering lead summarizing the status of an Azure DevOps pull request.");
        sb.AppendLine("You will be given a JSON object containing PR metadata, reviewer voting summary, and comment thread counts, plus linked work item info.");
        sb.AppendLine();
        sb.AppendLine("Write a concise status summary that answers: why is it still open, and what should happen next?");
        sb.AppendLine("Constraints:");
        sb.AppendLine("- Use ONLY the provided JSON data; do not invent details (no guessing about code changes, tests, or content).");
        sb.AppendLine("- If the reason can't be determined from the data, say so explicitly and name what is missing.");
        sb.AppendLine("- Mention concrete blockers (draft, rejected votes, waiting for author, active threads) if present.");
        sb.AppendLine("- Keep it short: 2 to 4 sentences, plain text, no markdown, no bullets.");
        return sb.ToString().Trim();
    }

    private static string BuildUserPrompt(string prSnapshotJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PR snapshot JSON:");
        sb.AppendLine(prSnapshotJson);
        return sb.ToString();
    }
}
