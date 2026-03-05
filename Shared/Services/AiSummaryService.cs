using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AzureDevOpsMcp.Shared.Services;

public sealed record AiPrAnalysis(string Summary, int ReadinessScore);

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

    public async Task<AiPrAnalysis> AnalyzePrAsync(string prSnapshotJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var completion = await _chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(BuildSystemPrompt()),
                    new UserChatMessage(BuildUserPrompt(prSnapshotJson)),
                ],
                null,
                cancellationToken
            );

            var text = completion?.Value?.Content?.FirstOrDefault()?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return new AiPrAnalysis("AI analysis not generated (empty model response).", 0);

            return ParseAnalysis(text);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Azure AI call failed (Model={Model}).", _settings.Model);
            return new AiPrAnalysis($"AI analysis not generated (Azure AI request failed: {ex.Status}).", 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure AI call failed (Model={Model}).", _settings.Model);
            return new AiPrAnalysis("AI analysis not generated (Azure AI request failed).", 0);
        }
    }

    private static AiPrAnalysis ParseAnalysis(string text)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(text.Trim());
        var root = doc.RootElement;
        return new AiPrAnalysis(
            root.GetProperty("summary").GetString() ?? "",
            Math.Clamp(root.GetProperty("readinessScore").GetInt32(), 0, 100)
        );
    }

    private static string BuildSystemPrompt() =>
        """
        You are a senior engineering lead analyzing the status of an Azure DevOps pull request.
        You will be given a JSON object with PR metadata, reviewer votes, comment thread counts, and linked work item info.

        Respond with ONLY a raw JSON object (no markdown, no code fences) with exactly these two fields:
          "summary": 2-4 sentence plain-text answer to: why is it still open and what should happen next?
          "readinessScore": integer 0-100 representing how close this PR is to being merged/closed.

        readinessScore guide:
          0-25  = heavily blocked (draft, rejected, many active threads, no reviewers)
          26-50 = some blockers remain (active threads, incomplete reviews, waiting for author)
          51-75 = mostly clear, minor items outstanding (single thread, some non-responses)
          76-99 = nearly ready (approved or approved with suggestions, no active threads)
          100   = fully approved with no blockers, ready to merge

        Constraints:
        - Use ONLY the provided JSON data.
        - If a reason cannot be determined, say so in summary and set readinessScore to 0.
        """;

    private static string BuildUserPrompt(string prSnapshotJson) =>
        $"""
        PR snapshot JSON:
        {prSnapshotJson}
        """;
}
