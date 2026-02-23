using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Infra.Tools;

[McpServerToolType]
public class GitTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "get_repo_by_name_or_id")]
    [Description("Get repository details by name or ID.")]
    public async Task<GitRepository> GetRepoByNameOrId(
        [Description("The repository name or ID.")] string repositoryNameOrId
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetRepositoryAsync(project, repositoryNameOrId);
    }

    [McpServerTool(Name = "list_branches")]
    [Description("List all branches in a Git repository.")]
    public async Task<IEnumerable<GitBranchStats>> ListBranches(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("Filter branches containing this text.")] string filterContains = null,
        [Description("Maximum number of branches to return.")] int top = 100
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var branches = await client.GetBranchesAsync(repositoryId, project);

        IEnumerable<GitBranchStats> result = branches ?? Enumerable.Empty<GitBranchStats>();

        if (!string.IsNullOrEmpty(filterContains))
            result = result.Where(b =>
                b.Name.Contains(filterContains, StringComparison.OrdinalIgnoreCase)
            );

        return result.Take(top);
    }

    [McpServerTool(Name = "list_my_branches")]
    [Description("List branches created by the current user.")]
    public async Task<string> ListMyBranches(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("Filter branches containing this text.")] string filterContains = null,
        [Description("Maximum number of branches to return.")] int top = 100
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var refs = await client.GetRefsAsync(
            repositoryId,
            project,
            filter: "heads",
            filterContains: filterContains,
            peelTags: true,
            top: top
        );
        return JsonSerializer.Serialize(refs, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_branch")]
    [Description("Get details of a specific branch including commit info.")]
    public async Task<GitBranchStats> GetBranch(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The name of the branch.")] string branchName
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetBranchAsync(repositoryId, branchName, project);
    }

    [McpServerTool(Name = "get_file_content")]
    [Description("Get the content of a file from a repository at a specific branch or commit.")]
    public async Task<GitItem> GetFileContent(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The path to the file in the repository.")] string path,
        [Description("The branch name or commit SHA to get the file from.")]
            string version = "main",
        [Description("The type of version: 'branch', 'commit', or 'tag'.")]
            string versionType = "branch"
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var versionDescriptor = new GitVersionDescriptor
        {
            Version = version,
            VersionType = versionType.ToLower() switch
            {
                "commit" => GitVersionType.Commit,
                "tag" => GitVersionType.Tag,
                _ => GitVersionType.Branch,
            },
        };

        return await client.GetItemAsync(
            project: project,
            repositoryId: repositoryId,
            path: path,
            versionDescriptor: versionDescriptor,
            includeContent: true
        );
    }

    [McpServerTool(Name = "list_pull_requests")]
    [Description("List pull requests in a repository with filtering options.")]
    public async Task<IEnumerable<GitPullRequest>> ListPullRequests(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("Filter by status: 'active', 'completed', 'abandoned', or 'all'.")]
            string status = "active",
        [Description("Filter by target branch.")] string targetBranch = "",
        [Description("Filter by source branch.")] string sourceBranch = "",
        [Description("Filter by creator email or unique name.")] string createdByUser = null,
        [Description("Maximum number of pull requests to return.")] int top = 50,
        [Description("Number of pull requests to skip.")] int skip = 0
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var searchCriteria = new GitPullRequestSearchCriteria
        {
            Status = status.ToLower() switch
            {
                "completed" => PullRequestStatus.Completed,
                "abandoned" => PullRequestStatus.Abandoned,
                "all" => PullRequestStatus.All,
                _ => PullRequestStatus.Active,
            },
        };

        if (!string.IsNullOrEmpty(targetBranch))
            searchCriteria.TargetRefName = $"refs/heads/{targetBranch}";

        if (!string.IsNullOrEmpty(sourceBranch))
            searchCriteria.SourceRefName = $"refs/heads/{sourceBranch}";

        if (!string.IsNullOrEmpty(createdByUser))
            searchCriteria.CreatorId = Guid.TryParse(createdByUser, out var guid) ? guid : default;

        var prs = await client.GetPullRequestsAsync(
            project,
            repositoryId,
            searchCriteria,
            top: top,
            skip: skip
        );
        return prs ?? Enumerable.Empty<GitPullRequest>();
    }

    [McpServerTool(Name = "get_pull_request_commits")]
    [Description("Get the commits associated with a pull request.")]
    public async Task<IEnumerable<GitCommitRef>> GetPullRequestCommits(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var commits = await client.GetPullRequestCommitsAsync(
            repositoryId,
            pullRequestId,
            project
        );
        return commits ?? Enumerable.Empty<GitCommitRef>();
    }

    [McpServerTool(Name = "get_pull_request_reviewers")]
    [Description("Get the reviewers of a pull request.")]
    public async Task<IEnumerable<IdentityRefWithVote>> GetPullRequestReviewers(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var reviewers = await client.GetPullRequestReviewersAsync(
            repositoryId,
            pullRequestId,
            project
        );
        return reviewers ?? Enumerable.Empty<IdentityRefWithVote>();
    }

    [McpServerTool(Name = "update_pull_request")]
    [Description("Update a pull request (title, description, status, etc.).")]
    public async Task<GitPullRequest> UpdatePullRequest(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("New title for the PR.")] string title = "",
        [Description("New description for the PR.")] string description = "",
        [Description("New status: 'active', 'completed', 'abandoned'.")] string status = "",
        [Description("New target branch.")] string targetRefName = "",
        [Description("Whether the PR is a draft.")] bool? isDraft = null
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var update = new GitPullRequest();
        if (!string.IsNullOrEmpty(title))
            update.Title = title;
        if (!string.IsNullOrEmpty(description))
            update.Description = description;
        if (!string.IsNullOrEmpty(targetRefName))
            update.TargetRefName = $"refs/heads/{targetRefName}";
        if (isDraft.HasValue)
            update.IsDraft = isDraft.Value;
        if (!string.IsNullOrEmpty(status))
        {
            update.Status = status.ToLower() switch
            {
                "completed" => PullRequestStatus.Completed,
                "abandoned" => PullRequestStatus.Abandoned,
                _ => PullRequestStatus.Active,
            };
        }

        return await client.UpdatePullRequestAsync(
            update,
            repositoryId,
            pullRequestId,
            project
        );
    }

    [McpServerTool(Name = "update_pull_request_reviewers")]
    [Description("Add or remove reviewers from a pull request.")]
    public async Task<string> UpdatePullRequestReviewers(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("List of reviewer IDs.")] IEnumerable<string> reviewerIds,
        [Description("Action: 'add' or 'remove'.")] string action = "add"
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var results = new List<object>();

        foreach (var reviewerId in reviewerIds)
        {
            if (action.ToLower() == "remove")
            {
                await client.DeletePullRequestReviewerAsync(
                    repositoryId,
                    pullRequestId,
                    reviewerId,
                    project
                );
                results.Add(new { reviewerId, action = "removed" });
            }
            else
            {
                var reviewer = new IdentityRefWithVote { Id = reviewerId };
                var result = await client.CreatePullRequestReviewerAsync(
                    reviewer,
                    repositoryId,
                    pullRequestId,
                    reviewerId,
                    project
                );
                results.Add(result);
            }
        }

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    [McpServerTool(Name = "add_pull_request_reviewer")]
    [Description("Add a reviewer to a pull request.")]
    public async Task<IdentityRefWithVote> AddPullRequestReviewer(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("The ID of the reviewer to add.")] string reviewerId,
        [Description("Whether the reviewer is required.")] bool isRequired = false
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var reviewer = new IdentityRefWithVote { Id = reviewerId, IsRequired = isRequired };

        return await client.CreatePullRequestReviewerAsync(
            reviewer,
            repositoryId,
            pullRequestId,
            reviewerId,
            project
        );
    }

    [McpServerTool(Name = "get_pull_request_threads")]
    [Description("Get all comment threads on a pull request.")]
    public async Task<IEnumerable<GitPullRequestCommentThread>> GetPullRequestThreads(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("Filter by status: 'active', 'fixed', 'wontFix', 'closed', 'pending'.")]
            string status = null
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var threads = await client.GetThreadsAsync(project, repositoryId, pullRequestId);

        var result = threads ?? Enumerable.Empty<GitPullRequestCommentThread>();

        if (!string.IsNullOrEmpty(status))
        {
            var statusEnum = status.ToLower() switch
            {
                "fixed" => CommentThreadStatus.Fixed,
                "wontfix" => CommentThreadStatus.WontFix,
                "closed" => CommentThreadStatus.Closed,
                "pending" => CommentThreadStatus.Pending,
                _ => CommentThreadStatus.Active,
            };
            result = result.Where(t => t.Status == statusEnum);
        }

        return result;
    }

    [McpServerTool(Name = "list_pull_request_thread_comments")]
    [Description("Get comments in a specific thread.")]
    public async Task<IEnumerable<Comment>> ListPullRequestThreadComments(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("The ID of the thread.")] int threadId
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;
        var comments = await client.GetCommentsAsync(
            repositoryId,
            pullRequestId,
            threadId,
            project
        );
        return comments ?? Enumerable.Empty<Comment>();
    }

    [McpServerTool(Name = "create_pull_request_thread")]
    [Description("Create a new comment thread on a pull request.")]
    public async Task<GitPullRequestCommentThread> CreatePullRequestThread(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("The content of the comment.")] string content,
        [Description("The file path for file-level comments.")] string filePath = null,
        [Description(
            "The status of the thread: 'active', 'fixed', 'wontFix', 'closed', 'pending'."
        )]
            string status = "active"
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var thread = new GitPullRequestCommentThread
        {
            Comments = new List<Comment> { new Comment { Content = content } },
            Status = status.ToLower() switch
            {
                "fixed" => CommentThreadStatus.Fixed,
                "wontfix" => CommentThreadStatus.WontFix,
                "closed" => CommentThreadStatus.Closed,
                "pending" => CommentThreadStatus.Pending,
                _ => CommentThreadStatus.Active,
            },
        };

        if (!string.IsNullOrEmpty(filePath))
        {
            thread.ThreadContext = new CommentThreadContext { FilePath = filePath };
        }

        return await client.CreateThreadAsync(
            thread,
            repositoryId,
            pullRequestId,
            project
        );
    }

    [McpServerTool(Name = "update_pull_request_thread")]
    [Description("Update an existing comment thread on a pull request.")]
    public async Task<GitPullRequestCommentThread> UpdatePullRequestThread(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("The ID of the thread.")] int threadId,
        [Description("New status: 'active', 'fixed', 'wontFix', 'closed', 'pending'.")]
            string status
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var project = _adoService.DefaultProject;

        var threadUpdate = new GitPullRequestCommentThread
        {
            Status = status.ToLower() switch
            {
                "fixed" => CommentThreadStatus.Fixed,
                "wontfix" => CommentThreadStatus.WontFix,
                "closed" => CommentThreadStatus.Closed,
                "pending" => CommentThreadStatus.Pending,
                _ => CommentThreadStatus.Active,
            },
        };

        return await client.UpdateThreadAsync(
            threadUpdate,
            repositoryId,
            pullRequestId,
            threadId,
            project
        );
    }

    [McpServerTool(Name = "reply_to_comment")]
    [Description("Reply to a comment in a pull request thread.")]
    public async Task<Comment> ReplyToComment(
        [Description("The ID or name of the repository.")] string repositoryId,
        [Description("The ID of the pull request.")] int pullRequestId,
        [Description("The ID of the thread.")] int threadId,
        [Description("The content of the reply.")] string content
    )
    {
        var client = await _adoService.GetGitApiAsync();
        var comment = new Comment { Content = content };
        var project = _adoService.DefaultProject;
        return await client.CreateCommentAsync(
            comment,
            repositoryId,
            pullRequestId,
            threadId,
            project
        );
    }
}