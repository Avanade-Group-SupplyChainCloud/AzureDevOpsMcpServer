using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace AzureDevOpsMcp.Infra.Tools
{
    [McpServerToolType]
    public class GitTools
    {
        private readonly AzureDevOpsService _adoService;

        public GitTools(AzureDevOpsService adoService)
        {
            _adoService = adoService;
        }

        [McpServerTool(Name = "get_repo_by_name_or_id")]
        [Description("Get repository details by name or ID.")]
        public async Task<GitRepository> GetRepoByNameOrId(
            [Description("The project name or ID.")] string project,
            [Description("The repository name or ID.")] string repositoryNameOrId)
        {
            var client = await _adoService.GetGitApiAsync();
            return await client.GetRepositoryAsync(project, repositoryNameOrId);
        }

        [McpServerTool(Name = "list_branches")]
        [Description("List all branches in a Git repository.")]
        public async Task<IEnumerable<GitBranchStats>> ListBranches(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The project name or ID.")] string project,
            [Description("Filter branches containing this text.")] string? filterContains = null,
            [Description("Maximum number of branches to return.")] int top = 100)
        {
            var client = await _adoService.GetGitApiAsync();
            var branches = await client.GetBranchesAsync(repositoryId, project);
            
            IEnumerable<GitBranchStats> result = branches ?? Enumerable.Empty<GitBranchStats>();
            
            if (!string.IsNullOrEmpty(filterContains))
                result = result.Where(b => b.Name.Contains(filterContains, StringComparison.OrdinalIgnoreCase));
            
            return result.Take(top);
        }

        [McpServerTool(Name = "list_my_branches")]
        [Description("List branches created by the current user.")]
        public async Task<string> ListMyBranches(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The project name or ID.")] string project,
            [Description("Filter branches containing this text.")] string? filterContains = null,
            [Description("Maximum number of branches to return.")] int top = 100)
        {
            var connection = _adoService.Connection;
            var baseUrl = connection.Uri.ToString().TrimEnd('/');
            var url = $"{baseUrl}/{project}/_apis/git/repositories/{repositoryId}/refs?filter=heads&peelTags=true&api-version=7.1-preview.1";
            
            if (!string.IsNullOrEmpty(filterContains))
                url += $"&filterContains={Uri.EscapeDataString(filterContains)}";

            using var httpClient = _adoService.CreateHttpClient();
            
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                return $"Error listing branches: {response.StatusCode} - {content}";
                
            return content;
        }

        [McpServerTool(Name = "get_branch")]
        [Description("Get details of a specific branch including commit info.")]
        public async Task<GitBranchStats> GetBranch(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The name of the branch.")] string branchName,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            return await client.GetBranchAsync(repositoryId, branchName, project);
        }

        [McpServerTool(Name = "list_commits")]
        [Description("List commits in a repository, optionally filtered by branch.")]
        public async Task<IEnumerable<GitCommitRef>> ListCommits(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The project name or ID.")] string project,
            [Description("The branch name to filter commits.")] string branch = "main",
            [Description("Maximum number of commits to return.")] int top = 50,
            [Description("Number of commits to skip.")] int skip = 0)
        {
            var client = await _adoService.GetGitApiAsync();
            var searchCriteria = new GitQueryCommitsCriteria
            {
                Top = top,
                Skip = skip,
                ItemVersion = new GitVersionDescriptor
                {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                }
            };

            var commits = await client.GetCommitsAsync(project, repositoryId, searchCriteria);
            return commits ?? Enumerable.Empty<GitCommitRef>();
        }

        [McpServerTool(Name = "search_commits")]
        [Description("Search for commits with comprehensive filtering capabilities.")]
        public async Task<IEnumerable<GitCommitRef>> SearchCommits(
            [Description("The project name or ID.")] string project,
            [Description("The repository name or ID.")] string repository,
            [Description("Search text in commit messages.")] string? searchText = null,
            [Description("Filter by author.")] string? author = null,
            [Description("From date (ISO format).")] DateTime? fromDate = null,
            [Description("To date (ISO format).")] DateTime? toDate = null,
            [Description("Branch or commit version.")] string? version = null,
            [Description("Version type: 'branch', 'commit', 'tag'.")] string versionType = "branch",
            [Description("Maximum results.")] int top = 50,
            [Description("Results to skip.")] int skip = 0)
        {
            var client = await _adoService.GetGitApiAsync();
            var searchCriteria = new GitQueryCommitsCriteria
            {
                Top = top,
                Skip = skip,
                Author = author,
                FromDate = fromDate?.ToString("o"),
                ToDate = toDate?.ToString("o")
            };

            if (!string.IsNullOrEmpty(version))
            {
                searchCriteria.ItemVersion = new GitVersionDescriptor
                {
                    Version = version,
                    VersionType = versionType.ToLower() switch
                    {
                        "commit" => GitVersionType.Commit,
                        "tag" => GitVersionType.Tag,
                        _ => GitVersionType.Branch
                    }
                };
            }

            var commits = await client.GetCommitsAsync(project, repository, searchCriteria);
            
            var result = commits ?? Enumerable.Empty<GitCommitRef>();
            if (!string.IsNullOrEmpty(searchText))
                result = result.Where(c => c.Comment?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true);
            
            return result;
        }

        [McpServerTool(Name = "get_commit")]
        [Description("Get details of a specific commit by its SHA.")]
        public async Task<GitCommit> GetCommit(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The commit SHA.")] string commitId,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            return await client.GetCommitAsync(commitId, repositoryId, project);
        }

        [McpServerTool(Name = "get_file_content")]
        [Description("Get the content of a file from a repository at a specific branch or commit.")]
        public async Task<GitItem> GetFileContent(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The path to the file in the repository.")] string path,
            [Description("The project name or ID.")] string project,
            [Description("The branch name or commit SHA to get the file from.")] string version = "main",
            [Description("The type of version: 'branch', 'commit', or 'tag'.")] string versionType = "branch")
        {
            var client = await _adoService.GetGitApiAsync();

            var versionDescriptor = new GitVersionDescriptor
            {
                Version = version,
                VersionType = versionType.ToLower() switch
                {
                    "commit" => GitVersionType.Commit,
                    "tag" => GitVersionType.Tag,
                    _ => GitVersionType.Branch
                }
            };

            return await client.GetItemAsync(project: project, repositoryId: repositoryId, path: path, versionDescriptor: versionDescriptor, includeContent: true);
        }

        [McpServerTool(Name = "list_pull_requests")]
        [Description("List pull requests in a repository with filtering options.")]
        public async Task<IEnumerable<GitPullRequest>> ListPullRequests(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The project name or ID.")] string project,
            [Description("Filter by status: 'active', 'completed', 'abandoned', or 'all'.")] string status = "active",
            [Description("Filter by target branch.")] string targetBranch = "",
            [Description("Filter by source branch.")] string sourceBranch = "",
            [Description("Filter by creator email or unique name.")] string? createdByUser = null,
            [Description("Maximum number of pull requests to return.")] int top = 50,
            [Description("Number of pull requests to skip.")] int skip = 0)
        {
            var client = await _adoService.GetGitApiAsync();

            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = status.ToLower() switch
                {
                    "completed" => PullRequestStatus.Completed,
                    "abandoned" => PullRequestStatus.Abandoned,
                    "all" => PullRequestStatus.All,
                    _ => PullRequestStatus.Active
                }
            };

            if (!string.IsNullOrEmpty(targetBranch))
                searchCriteria.TargetRefName = $"refs/heads/{targetBranch}";
            
            if (!string.IsNullOrEmpty(sourceBranch))
                searchCriteria.SourceRefName = $"refs/heads/{sourceBranch}";
            
            if (!string.IsNullOrEmpty(createdByUser))
                searchCriteria.CreatorId = Guid.TryParse(createdByUser, out var guid) ? guid : default;

            var prs = await client.GetPullRequestsAsync(project, repositoryId, searchCriteria, top: top, skip: skip);
            return prs ?? Enumerable.Empty<GitPullRequest>();
        }

        [McpServerTool(Name = "list_pull_requests_by_commits")]
        [Description("Find pull requests containing specific commits.")]
        public async Task<string> ListPullRequestsByCommits(
            [Description("The project name or ID.")] string project,
            [Description("The repository name or ID.")] string repository,
            [Description("Array of commit IDs to query for.")] IEnumerable<string> commits)
        {
            // Using REST API since GitPullRequestQuery properties differ in SDK versions
            var connection = _adoService.Connection;
            var baseUrl = connection.Uri.ToString().TrimEnd('/');
            var url = $"{baseUrl}/{project}/_apis/git/repositories/{repository}/pullrequestquery?api-version=7.1";

            var body = new
            {
                queries = new[]
                {
                    new
                    {
                        type = "lastMergeCommit",
                        items = commits.ToArray()
                    }
                }
            };

            using var httpClient = _adoService.CreateHttpClient();
            var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                return $"Error querying pull requests by commits: {response.StatusCode} - {responseContent}";
                
            return responseContent;
        }

        [McpServerTool(Name = "get_pull_request_commits")]
        [Description("Get the commits associated with a pull request.")]
        public async Task<IEnumerable<GitCommitRef>> GetPullRequestCommits(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            var commits = await client.GetPullRequestCommitsAsync(repositoryId, pullRequestId, project);
            return commits ?? Enumerable.Empty<GitCommitRef>();
        }

        [McpServerTool(Name = "get_pull_request_reviewers")]
        [Description("Get the reviewers of a pull request.")]
        public async Task<IEnumerable<IdentityRefWithVote>> GetPullRequestReviewers(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            var reviewers = await client.GetPullRequestReviewersAsync(repositoryId, pullRequestId, project);
            return reviewers ?? Enumerable.Empty<IdentityRefWithVote>();
        }

        [McpServerTool(Name = "update_pull_request")]
        [Description("Update a pull request (title, description, status, etc.).")]
        public async Task<GitPullRequest> UpdatePullRequest(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The project name or ID.")] string project,
            [Description("New title for the PR.")] string title = "",
            [Description("New description for the PR.")] string description = "",
            [Description("New status: 'active', 'completed', 'abandoned'.")] string status = "",
            [Description("New target branch.")] string targetRefName = "",
            [Description("Whether the PR is a draft.")] bool? isDraft = null)
        {
            var client = await _adoService.GetGitApiAsync();

            var update = new GitPullRequest();
            if (!string.IsNullOrEmpty(title)) update.Title = title;
            if (!string.IsNullOrEmpty(description)) update.Description = description;
            if (!string.IsNullOrEmpty(targetRefName)) update.TargetRefName = $"refs/heads/{targetRefName}";
            if (isDraft.HasValue) update.IsDraft = isDraft.Value;
            if (!string.IsNullOrEmpty(status))
            {
                update.Status = status.ToLower() switch
                {
                    "completed" => PullRequestStatus.Completed,
                    "abandoned" => PullRequestStatus.Abandoned,
                    _ => PullRequestStatus.Active
                };
            }

            return await client.UpdatePullRequestAsync(update, repositoryId, pullRequestId, project);
        }

        [McpServerTool(Name = "update_pull_request_reviewers")]
        [Description("Add or remove reviewers from a pull request.")]
        public async Task<string> UpdatePullRequestReviewers(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The project name or ID.")] string project,
            [Description("List of reviewer IDs.")] IEnumerable<string> reviewerIds,
            [Description("Action: 'add' or 'remove'.")] string action = "add")
        {
            var client = await _adoService.GetGitApiAsync();
            var results = new List<object>();

            foreach (var reviewerId in reviewerIds)
            {
                if (action.ToLower() == "remove")
                {
                    await client.DeletePullRequestReviewerAsync(repositoryId, pullRequestId, reviewerId, project);
                    results.Add(new { reviewerId, action = "removed" });
                }
                else
                {
                    var reviewer = new IdentityRefWithVote { Id = reviewerId };
                    var result = await client.CreatePullRequestReviewerAsync(reviewer, repositoryId, pullRequestId, reviewerId, project);
                    results.Add(result);
                }
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "add_pull_request_reviewer")]
        [Description("Add a reviewer to a pull request.")]
        public async Task<IdentityRefWithVote> AddPullRequestReviewer(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The ID of the reviewer to add.")] string reviewerId,
            [Description("The project name or ID.")] string project,
            [Description("Whether the reviewer is required.")] bool isRequired = false)
        {
            var client = await _adoService.GetGitApiAsync();

            var reviewer = new IdentityRefWithVote
            {
                Id = reviewerId,
                IsRequired = isRequired
            };

            return await client.CreatePullRequestReviewerAsync(reviewer, repositoryId, pullRequestId, reviewerId, project);
        }

        [McpServerTool(Name = "get_pull_request_threads")]
        [Description("Get all comment threads on a pull request.")]
        public async Task<IEnumerable<GitPullRequestCommentThread>> GetPullRequestThreads(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The project name or ID.")] string project,
            [Description("Filter by status: 'active', 'fixed', 'wontFix', 'closed', 'pending'.")] string? status = null)
        {
            var client = await _adoService.GetGitApiAsync();
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
                    _ => CommentThreadStatus.Active
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
            [Description("The ID of the thread.")] int threadId,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            var comments = await client.GetCommentsAsync(repositoryId, pullRequestId, threadId, project);
            return comments ?? Enumerable.Empty<Comment>();
        }

        [McpServerTool(Name = "create_pull_request_thread")]
        [Description("Create a new comment thread on a pull request.")]
        public async Task<GitPullRequestCommentThread> CreatePullRequestThread(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The content of the comment.")] string content,
            [Description("The project name or ID.")] string project,
            [Description("The file path for file-level comments.")] string? filePath = null,
            [Description("The status of the thread: 'active', 'fixed', 'wontFix', 'closed', 'pending'.")] string status = "active")
        {
            var client = await _adoService.GetGitApiAsync();

            var thread = new GitPullRequestCommentThread
            {
                Comments = new List<Comment>
                {
                    new Comment { Content = content }
                },
                Status = status.ToLower() switch
                {
                    "fixed" => CommentThreadStatus.Fixed,
                    "wontfix" => CommentThreadStatus.WontFix,
                    "closed" => CommentThreadStatus.Closed,
                    "pending" => CommentThreadStatus.Pending,
                    _ => CommentThreadStatus.Active
                }
            };

            if (!string.IsNullOrEmpty(filePath))
            {
                thread.ThreadContext = new CommentThreadContext
                {
                    FilePath = filePath
                };
            }

            return await client.CreateThreadAsync(thread, repositoryId, pullRequestId, project);
        }

        [McpServerTool(Name = "update_pull_request_thread")]
        [Description("Update an existing comment thread on a pull request.")]
        public async Task<GitPullRequestCommentThread> UpdatePullRequestThread(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The ID of the thread.")] int threadId,
            [Description("The project name or ID.")] string project,
            [Description("New status: 'active', 'fixed', 'wontFix', 'closed', 'pending'.")] string status)
        {
            var client = await _adoService.GetGitApiAsync();

            var threadUpdate = new GitPullRequestCommentThread
            {
                Status = status.ToLower() switch
                {
                    "fixed" => CommentThreadStatus.Fixed,
                    "wontfix" => CommentThreadStatus.WontFix,
                    "closed" => CommentThreadStatus.Closed,
                    "pending" => CommentThreadStatus.Pending,
                    _ => CommentThreadStatus.Active
                }
            };

            return await client.UpdateThreadAsync(threadUpdate, repositoryId, pullRequestId, threadId, project);
        }

        [McpServerTool(Name = "reply_to_comment")]
        [Description("Reply to a comment in a pull request thread.")]
        public async Task<Comment> ReplyToComment(
            [Description("The ID or name of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId,
            [Description("The ID of the thread.")] int threadId,
            [Description("The content of the reply.")] string content,
            [Description("The project name or ID.")] string project)
        {
            var client = await _adoService.GetGitApiAsync();
            var comment = new Comment { Content = content };
            return await client.CreateCommentAsync(comment, repositoryId, pullRequestId, threadId, project);
        }
    }
}
