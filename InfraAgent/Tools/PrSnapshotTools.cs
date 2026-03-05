using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Infra.Tools;

[McpServerToolType]
public class PrSnapshotTools(AzureDevOpsService adoService, AiSummaryService aiSummaryService)
{
    private readonly AzureDevOpsService _adoService = adoService;
    private readonly AiSummaryService _aiSummaryService = aiSummaryService;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "get_pr_area_snapshot")]
    [Description(
        "Get a snapshot of all open pull requests for the default Azure DevOps project. "
        + "Returns each PR with thread counts (active/resolved/closed), linked work item details (when present), "
        + "work item hierarchy (when present), reviewer status, and an Azure AI generated summary "
        + "explaining why the PR is still open or what should happen next."
    )]
    public async Task<string> GetPrAreaSnapshot(
        [Description(
            "An area path label to include in the response (e.g. 'MyProject\\MyTeam'). "
            + "This tool currently does not filter by area path; it is returned for caller context."
        )]
            string areaPath = ""
    )
    {
        var gitClient = await _adoService.GetGitApiAsync();
        var witClient = await _adoService.GetWorkItemTrackingApiAsync();
        var project = _adoService.DefaultProject;

        // TODO: Pull required-approver count dynamically from branch policies.
        // For now, keep it simple and assume 2 approvals are required.
        const int minApproverCount = 2;

        // Pull all active PRs across the project (over-fetch to allow filtering by area)
        var searchCriteria = new GitPullRequestSearchCriteria
        {
            Status = PullRequestStatus.Active,
        };

        var allPrs = await gitClient.GetPullRequestsByProjectAsync(
            project,
            searchCriteria,
            top: null
        );

        if (allPrs == null || allPrs.Count == 0)
            return JsonSerializer.Serialize(
                new { area = areaPath, snapshotDate = DateTime.UtcNow, totalOpenPrs = 0, pullRequests = Array.Empty<object>() },
                JsonOptions
            );

        var snapshotItems = new List<(int readinessScore, object entry)>();

        foreach (var pr in allPrs)
        {
            var repoId = pr.Repository?.Id.ToString();
            if (string.IsNullOrWhiteSpace(repoId))
                continue;

            var targetRefName = pr.TargetRefName ?? "";

            // Get work items linked to the PR (optional)
            IEnumerable<ResourceRef> wiRefs = null;
            try
            {
                wiRefs = await gitClient.GetPullRequestWorkItemRefsAsync(repoId, pr.PullRequestId, project);
            }
            catch
            {
                wiRefs = null;
            }

            // Parse work item IDs from resource refs (format: "vstfs:///WorkItemTracking/WorkItem/{id}")
            var wiIds = wiRefs
                .Select(r => ExtractWorkItemId(r.Id))
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            IList<WorkItem> workItems = null;
            if (wiIds.Length > 0)
            {
                try
                {
                    workItems = await witClient.GetWorkItemsAsync(project, wiIds, expand: WorkItemExpand.Relations);
                }
                catch
                {
                    workItems = null;
                }
            }

            // --- Thread counts ---
            var threadCounts = await GetThreadCountsAsync(gitClient, project, repoId, pr.PullRequestId);

            // --- Reviewer summary ---
            var reviewerInfo = BuildReviewerInfo(pr.Reviewers);

            WorkItem primaryWi = workItems?.FirstOrDefault();
            var hierarchy = primaryWi != null
                ? await BuildHierarchyAsync(witClient, project, primaryWi)
                : [];

            var linkedAreaPaths = workItems == null
                ? ""
                : string.Join(", ", workItems.Select(w => GetField(w, "System.AreaPath")).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct());

            object linkedWiSnapshot = null;
            string linkedWorkItemUrl = null;
            if (primaryWi != null)
            {
                linkedWorkItemUrl = primaryWi.Id != null
                    ? BuildWorkItemUrl(project, primaryWi.Id.Value)
                    : null;

                linkedWiSnapshot = new
                {
                    id = primaryWi.Id,
                    title = GetField(primaryWi, "System.Title"),
                    type = GetField(primaryWi, "System.WorkItemType"),
                    state = GetField(primaryWi, "System.State"),
                    areaPath = GetField(primaryWi, "System.AreaPath"),
                    url = linkedWorkItemUrl,
                };
            }

            // --- Age ---
            var ageDays = (int)(DateTime.UtcNow - pr.CreationDate).TotalDays;

            // --- AI summary ---
            var aiSummaryInput = new
            {
                prId = pr.PullRequestId,
                title = pr.Title,
                description = pr.Description,
                repository = pr.Repository?.Name,
                createdBy = pr.CreatedBy?.DisplayName,
                creationDate = pr.CreationDate.ToString("o"),
                ageDays,
                isDraft = pr.IsDraft == true,
                targetRefName,
                reviewers = reviewerInfo,
                minApproverCount,
                threadCounts,
                linkedWorkItem = linkedWiSnapshot,
                linkedWorkItemUrl,
                linkedWorkItemAreaPaths = linkedAreaPaths,
                workItemHierarchy = hierarchy,
            };

            var analysis = await _aiSummaryService.AnalyzePrAsync(
                JsonSerializer.Serialize(aiSummaryInput, JsonOptions)
            );

            snapshotItems.Add((analysis.ReadinessScore, new
            {
                prId = pr.PullRequestId,
                title = pr.Title,
                repository = pr.Repository?.Name,
                createdBy = pr.CreatedBy?.DisplayName,
                creationDate = pr.CreationDate.ToString("o"),
                ageDays,
                isDraft = pr.IsDraft == true,
                url = BuildPrUrl(project, repoId, pr.PullRequestId),
                reviewers = reviewerInfo,
                targetRefName,
                minApproverCount,
                threadCounts,
                linkedWorkItem = linkedWiSnapshot,
                linkedWorkItemUrl,
                linkedWorkItemAreaPaths = linkedAreaPaths,
                workItemHierarchy = hierarchy,
                aiSummary = analysis.Summary,
                readinessScore = analysis.ReadinessScore,
            }));
        }

        var pullRequests = snapshotItems
            .OrderByDescending(x => x.readinessScore)
            .Select(x => x.entry)
            .ToList();

        var snapshot = new
        {
            area = areaPath,
            snapshotDate = DateTime.UtcNow.ToString("o"),
            totalOpenPrs = pullRequests.Count,
            pullRequests,
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string GetField(WorkItem wi, string fieldName) =>
        wi.Fields != null && wi.Fields.TryGetValue(fieldName, out var val) ? val?.ToString() : null;

    private static int ExtractWorkItemId(string resourceRefId)
    {
        if (string.IsNullOrWhiteSpace(resourceRefId))
            return 0;

        // vstfs:///WorkItemTracking/WorkItem/12345  →  12345
        var lastSlash = resourceRefId.LastIndexOf('/');
        if (lastSlash >= 0 && int.TryParse(resourceRefId.AsSpan(lastSlash + 1), out var id))
            return id;

        // Plain numeric string fallback
        return int.TryParse(resourceRefId, out var plainId) ? plainId : 0;
    }

    private async Task<object> GetThreadCountsAsync(
        GitHttpClient gitClient,
        string project,
        string repoId,
        int prId
    )
    {
        try
        {
            var threads = await gitClient.GetThreadsAsync(project, repoId, prId);
            if (threads == null)
                return new { active = 0, resolved = 0, closed = 0, total = 0 };

            // Only count non-system (human) threads
            var humanThreads = threads
                .Where(t => t.Comments != null && t.Comments.Any(c => c.CommentType == Microsoft.TeamFoundation.SourceControl.WebApi.CommentType.Text))
                .ToList();

            return new
            {
                active = humanThreads.Count(t => t.Status == CommentThreadStatus.Active || t.Status == CommentThreadStatus.Pending),
                resolved = humanThreads.Count(t => t.Status == CommentThreadStatus.Fixed),
                closed = humanThreads.Count(t => t.Status == CommentThreadStatus.Closed || t.Status == CommentThreadStatus.WontFix),
                total = humanThreads.Count,
            };
        }
        catch
        {
            return new { active = 0, resolved = 0, closed = 0, total = 0 };
        }
    }

    private static object BuildReviewerInfo(IList<IdentityRefWithVote> reviewers)
    {
        if (reviewers == null || reviewers.Count == 0)
            return new { total = 0, approved = 0, approvedWithSuggestions = 0, waitingForAuthor = 0, noResponse = 0, rejected = 0, summary = "No reviewers assigned" };

        // Azure DevOps often returns both "container" reviewers (e.g. groups/teams) and individual users.
        // To match what humans expect, count unique non-container reviewers when present.
        var effectiveReviewers = reviewers
            .Where(r => r != null)
            .Where(r => r.IsContainer != true)
            .GroupBy(r => r.Id ?? r.UniqueName ?? r.DisplayName ?? string.Empty)
            .Select(g => g.OrderByDescending(r => r.Vote).First())
            .ToList();

        // Fallback: if we only have container reviewers (no humans yet), count them instead.
        if (effectiveReviewers.Count == 0)
            effectiveReviewers = reviewers.Where(r => r != null).ToList();

        // ADO vote values: 10 = approved, 5 = approved with suggestions, -5 = waiting for author, 0 = no vote, -10 = rejected
        var approved = effectiveReviewers.Count(r => r.Vote == 10);
        var approvedWithSuggestions = effectiveReviewers.Count(r => r.Vote == 5);
        var waitingForAuthor = effectiveReviewers.Count(r => r.Vote == -5);
        var noResponse = effectiveReviewers.Count(r => r.Vote == 0);
        var rejected = effectiveReviewers.Count(r => r.Vote == -10);
        var approverCount = approved + approvedWithSuggestions;

        var parts = new List<string>();
        if (approved > 0) parts.Add($"{approved} approved");
        if (approvedWithSuggestions > 0) parts.Add($"{approvedWithSuggestions} approved with suggestions");
        if (waitingForAuthor > 0) parts.Add($"{waitingForAuthor} waiting for author");
        if (noResponse > 0) parts.Add($"{noResponse} no response");
        if (rejected > 0) parts.Add($"{rejected} rejected");

        return new
        {
            total = effectiveReviewers.Count,
            approved,
            approvedWithSuggestions,
            waitingForAuthor,
            noResponse,
            rejected,
            approverCount,
            summary = parts.Count > 0 ? string.Join(", ", parts) : "No votes yet",
        };
    }

    private async Task<List<object>> BuildHierarchyAsync(
        WorkItemTrackingHttpClient witClient,
        string project,
        WorkItem startItem
    )
    {
        var hierarchy = new List<object>();
        var current = startItem;
        var visitedIds = new HashSet<int>();

        // Walk up the parent chain, stopping at Feature (or max 5 levels)
        for (int depth = 0; depth < 5; depth++)
        {
            if (current?.Id == null || !visitedIds.Add(current.Id.Value))
                break;

            hierarchy.Insert(
                0,
                new
                {
                    id = current.Id,
                    type = GetField(current, "System.WorkItemType"),
                    title = GetField(current, "System.Title"),
                    state = GetField(current, "System.State"),
                }
            );

            // Stop if we've reached a Feature (or Epic)
            var wiType = GetField(current, "System.WorkItemType");
            if (wiType == "Feature" || wiType == "Epic")
                break;

            // Find parent relation
            var parentRelation = current.Relations?.FirstOrDefault(
                r => r.Rel == "System.LinkTypes.Hierarchy-Reverse"
            );

            if (parentRelation == null)
                break;

            var parentId = ExtractWorkItemId(parentRelation.Url);
            if (parentId <= 0)
                break;

            try
            {
                current = await witClient.GetWorkItemAsync(
                    project,
                    parentId,
                    expand: WorkItemExpand.Relations
                );
            }
            catch
            {
                break;
            }
        }

        return hierarchy;
    }

    private string BuildPrUrl(string project, string repoId, int prId)
    {
        var orgUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        return $"{orgUrl}/{Uri.EscapeDataString(project)}/_git/{repoId}/pullrequest/{prId}";
    }

    private string BuildWorkItemUrl(string project, int workItemId)
    {
        var orgUrl = _adoService.Connection.Uri.ToString().TrimEnd('/');
        return $"{orgUrl}/{Uri.EscapeDataString(project)}/_workitems/edit/{workItemId}";
    }

}
