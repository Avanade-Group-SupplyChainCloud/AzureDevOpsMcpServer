using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class WorkItemDeepDiveTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "work_item_deep_dive")]
    [Description(
        "Deep-dive a work item: returns all fields, description, repro steps, inline images as base64, "
        + "full discussion/comments, and every associated or mentioned pull request with PR details and comment threads."
    )]
    public async Task<string> WorkItemDeepDive(
        [Description("The ID of the work item to deep-dive into.")] int workItemId
    )
    {
        var witClient = await _adoService.GetWorkItemTrackingApiAsync();
        var gitClient = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        // 1. Get work item with relations (for artifact links to PRs)
        var workItem = await witClient.GetWorkItemAsync(project, workItemId, expand: WorkItemExpand.All);
        if (workItem == null)
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found." }, JsonOptions);

        // 2. Get discussion comments
        var comments = await witClient.GetCommentsAsync(project, workItemId, top: 200);

        // 3. Collect all PR references — scan every URL and text blob for both formats
        var prLinks = new List<PrRef>();

        // Scan all relation URLs (artifact links, hyperlinks, any link type)
        if (workItem.Relations != null)
            foreach (var rel in workItem.Relations)
                foreach (var pr in ExtractPrRefs(rel.Url))
                    AddPrIfNew(prLinks, pr);

        // Scan all text fields and discussion for PR references
        var textBlobs = new List<string>
        {
            GetField(workItem, "System.Description"),
            GetField(workItem, "Microsoft.VSTS.TCM.ReproSteps"),
        };
        if (comments?.Comments != null)
            textBlobs.AddRange(comments.Comments.Select(c => c.Text));

        foreach (var text in textBlobs)
            foreach (var pr in ExtractPrRefs(text))
                AddPrIfNew(prLinks, pr);

        // 4. Fetch inline images from HTML fields + discussion comments as base64
        var imageFields = new List<string>
        {
            GetField(workItem, "System.Description"),
            GetField(workItem, "Microsoft.VSTS.TCM.ReproSteps"),
            GetField(workItem, "Microsoft.VSTS.Common.AcceptanceCriteria"),
        };

        if (comments?.Comments != null)
            imageFields.AddRange(comments.Comments.Select(c => c.Text));

        var images = await FetchInlineImagesAsync(imageFields.ToArray());

        // 5. Fetch each PR's details + comment threads
        var pullRequests = new List<object>();
        foreach (var prRef in prLinks)
        {
            try
            {
                var repoId = prRef.RepoId;
                if (string.IsNullOrWhiteSpace(repoId))
                    repoId = await ResolveRepositoryIdForPullRequestAsync(project, prRef.PrId);

                var pr = await gitClient.GetPullRequestAsync(project, repoId, prRef.PrId);
                var threads = await gitClient.GetThreadsAsync(project, repoId, prRef.PrId);

                var threadData = BuildConversationalThreadData(threads);

                pullRequests.Add(new
                {
                    pr.PullRequestId,
                    pr.Title,
                    pr.Description,
                    Status = pr.Status.ToString(),
                    CreatedBy = pr.CreatedBy?.DisplayName,
                    CreationDate = pr.CreationDate.ToString("o"),
                    SourceBranch = pr.SourceRefName,
                    TargetBranch = pr.TargetRefName,
                    Repository = pr.Repository?.Name,
                    CommentThreads = threadData,
                });
            }
            catch (Exception ex)
            {
                pullRequests.Add(new
                {
                    PullRequestId = prRef.PrId,
                    RepoId = prRef.RepoId,
                    Error = ex.Message,
                });
            }
        }

        // 6. Build result
        var result = new
        {
            WorkItem = new
            {
                workItem.Id,
                Title = GetField(workItem, "System.Title"),
                Type = GetField(workItem, "System.WorkItemType"),
                State = GetField(workItem, "System.State"),
                Reason = GetField(workItem, "System.Reason"),
                AssignedTo = GetField(workItem, "System.AssignedTo"),
                CreatedBy = GetField(workItem, "System.CreatedBy"),
                CreatedDate = GetField(workItem, "System.CreatedDate"),
                ChangedDate = GetField(workItem, "System.ChangedDate"),
                IterationPath = GetField(workItem, "System.IterationPath"),
                AreaPath = GetField(workItem, "System.AreaPath"),
                Priority = GetField(workItem, "Microsoft.VSTS.Common.Priority"),
                Severity = GetField(workItem, "Microsoft.VSTS.Common.Severity"),
                Description = GetField(workItem, "System.Description"),
                ReproSteps = GetField(workItem, "Microsoft.VSTS.TCM.ReproSteps"),
                AcceptanceCriteria = GetField(workItem, "Microsoft.VSTS.Common.AcceptanceCriteria"),
                Tags = GetField(workItem, "System.Tags"),
            },
            Discussion = comments?.Comments?.Select(c => new
            {
                Author = c.CreatedBy?.DisplayName,
                c.Text,
                Date = c.CreatedDate == default ? null : c.CreatedDate.ToString("o"),
            }),
            PullRequests = pullRequests,
            InlineImages = images,
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    #region Helpers

    private static string GetField(WorkItem wi, string fieldName) =>
        wi.Fields != null && wi.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() : null;

    /// <summary>
    /// Extracts PR references from any string. Handles both formats:
    ///   - Artifact: vstfs:///Git/PullRequestId/{projectId}%2F{repoId}%2F{prId}
    ///   - URL:      _git/{repo}/pullrequest/{prId}
    /// </summary>
    private static List<PrRef> ExtractPrRefs(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var decoded = HttpUtility.UrlDecode(text);
        var results = new List<PrRef>();

        // Artifact link format: Git/PullRequestId/{projectId}/{repoId}/{prId}
        foreach (Match m in Regex.Matches(decoded, @"Git/PullRequestId/[^/]+/([^/]+)/(\d+)"))
            results.Add(new PrRef(m.Groups[1].Value, int.Parse(m.Groups[2].Value)));

        // URL format: _git/{repo}/pullrequest/{prId}
        foreach (Match m in Regex.Matches(decoded, @"_git/([^/""]+)/pullrequest/(\d+)", RegexOptions.IgnoreCase))
            results.Add(new PrRef(m.Groups[1].Value, int.Parse(m.Groups[2].Value)));

        // Mention format (no repo info): "Mentioned in !12624" or "PR 12624"
        // Avoid markdown image syntax: "![alt](...)".
        foreach (Match m in Regex.Matches(decoded, @"!(?!\[)(\d+)"))
            results.Add(new PrRef(null, int.Parse(m.Groups[1].Value)));

        foreach (Match m in Regex.Matches(decoded, @"\bPR\s+(\d+)\b", RegexOptions.IgnoreCase))
            results.Add(new PrRef(null, int.Parse(m.Groups[1].Value)));

        foreach (Match m in Regex.Matches(decoded, @"\bPull\s*request\s+(\d+)\b", RegexOptions.IgnoreCase))
            results.Add(new PrRef(null, int.Parse(m.Groups[1].Value)));

        return results;
    }

    private static void AddPrIfNew(List<PrRef> list, PrRef pr)
    {
        var existingIndex = list.FindIndex(p => p.PrId == pr.PrId);
        if (existingIndex < 0)
        {
            list.Add(pr);
            return;
        }

        // If we already have this PR id but without repo, prefer the version with repo id.
        var existing = list[existingIndex];
        if (string.IsNullOrWhiteSpace(existing.RepoId) && !string.IsNullOrWhiteSpace(pr.RepoId))
            list[existingIndex] = pr;
    }

    private static List<object> BuildConversationalThreadData(IEnumerable<GitPullRequestCommentThread> threads)
    {
        if (threads == null)
            return [];

        var result = new List<object>();

        foreach (var t in threads)
        {
            if (t?.Comments == null || t.Comments.Count == 0)
                continue;

            // Filter out system/bot noise and keep only meaningful discussion.
            var filtered = t.Comments
                .Where(c => !IsNoisePrComment(c))
                .Select(c => new
                {
                    Author = c.Author?.DisplayName,
                    Content = c.Content,
                    Date = c.PublishedDate == default ? null : c.PublishedDate.ToString("o"),
                })
                .ToList();

            if (filtered.Count < 2)
                continue;

            var distinctAuthors = filtered
                .Select(c => c.Author)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // Only include true back-and-forth (at least two distinct human authors).
            if (distinctAuthors < 2)
                continue;

            result.Add(new
            {
                ThreadId = t.Id,
                FilePath = t.ThreadContext?.FilePath,
                Comments = filtered,
            });
        }

        return result;
    }

    private static bool IsNoisePrComment(Microsoft.TeamFoundation.SourceControl.WebApi.Comment c)
    {
        if (c == null)
            return true;

        var author = c.Author?.DisplayName ?? string.Empty;
        var content = c.Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
            return true;

        // Drop common system/bot identities.
        if (author.Contains("Microsoft.VisualStudio.Services", StringComparison.OrdinalIgnoreCase))
            return true;

        // Drop common non-conversational automation updates.
        // (Keep it simple: match well-known phrases we see in ADO PR threads.)
        var lowered = content.Trim().ToLowerInvariant();

        if (lowered == "policy status has been updated")
            return true;

        if (Regex.IsMatch(lowered, @"\b(voted|vote of)\b"))
            return true;

        if (lowered.Contains("joined as a reviewer"))
            return true;
        if (lowered.Contains("published the pull request"))
            return true;
        if (lowered.Contains("set auto-complete") || lowered.Contains("set autocomplete"))
            return true;
        if (lowered.Contains("updated the pull request status"))
            return true;
        if (lowered.StartsWith("the reference refs/heads/") && lowered.Contains(" was updated"))
            return true;

        return false;
    }

    private async Task<string> ResolveRepositoryIdForPullRequestAsync(string project, int pullRequestId)
    {
        // REST: GET {org}/{project}/_apis/git/pullrequests/{pullRequestId}
        // Response contains repository.id which we need to query threads.
        var orgUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        var projectEncoded = Uri.EscapeDataString(project);
        var url = $"{orgUrl}/{projectEncoded}/_apis/git/pullrequests/{pullRequestId}?api-version=7.1";

        using var http = _adoService.CreateHttpClient();
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("repository", out var repoEl))
            throw new InvalidOperationException("PR response missing 'repository'.");
        if (!repoEl.TryGetProperty("id", out var idEl))
            throw new InvalidOperationException("PR response missing 'repository.id'.");

        var repoId = idEl.GetString();
        if (string.IsNullOrWhiteSpace(repoId))
            throw new InvalidOperationException("PR response 'repository.id' was empty.");
        return repoId;
    }

    /// <summary>Extract img src URLs from HTML fields, fetch them, return as base64.</summary>
    private async Task<List<object>> FetchInlineImagesAsync(string[] htmlFields)
    {
        var results = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var http = _adoService.CreateHttpClient();

        foreach (var html in htmlFields)
        {
            if (string.IsNullOrEmpty(html)) continue;

            foreach (Match m in Regex.Matches(html, @"<img[^>]+src=""([^""]+)""", RegexOptions.IgnoreCase))
            {
                var url = m.Groups[1].Value;
                if (!seen.Add(url)) continue;

                try
                {
                    using var response = await http.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (string.IsNullOrWhiteSpace(contentType))
                        contentType = url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            ? "image/png"
                            : "image/jpeg";
                    results.Add(new
                    {
                        Url = url,
                        Base64 = Convert.ToBase64String(bytes),
                        ContentType = contentType,
                    });
                }
                catch
                {
                    results.Add(new { Url = url, Error = "Could not fetch image" });
                }
            }
        }

        return results;
    }

    private record PrRef(string RepoId, int PrId);

    #endregion
}
