using AzureDevOpsMcpServer.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AzureDevOpsMcpServer.Tools
{
    [McpServerToolType]
    public class RepoTools
    {
        private readonly AzureDevOpsService _adoService;

        public RepoTools(AzureDevOpsService adoService)
        {
            _adoService = adoService;
        }

        [McpServerTool(Name = "list_repos")]
        [Description("List all Git repositories in a project. Returns repository ID, name, URL, and size.")]
        public async Task<IEnumerable<object>> ListReposByProject(
            [Description("The name or ID of the Azure DevOps project.")] string project,
            [Description("The maximum number of repositories to return. Defaults to 100.")] int top = 100,
            [Description("The number of repositories to skip for pagination. Defaults to 0.")] int skip = 0,
            [Description("Filter repositories by name. Supports partial matches.")] string? repoNameFilter = null)
        {
            var client = await _adoService.GetGitApiAsync();
            var repos = await client.GetRepositoriesAsync(project);

            if (repos == null)
            {
                return Enumerable.Empty<object>();
            }

            var filteredRepos = repos;
            if (!string.IsNullOrEmpty(repoNameFilter))
            {
                filteredRepos = repos.Where(r => r.Name.Contains(repoNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            filteredRepos = filteredRepos.OrderBy(r => r.Name).Skip(skip).Take(top).ToList();

            return filteredRepos.Select(r => new
            {
                r.Id,
                r.Name,
                r.IsDisabled,
                r.IsFork,
                r.WebUrl,
                r.Size
            });
        }

        [McpServerTool(Name = "create_pull_request")]
        [Description("Create a new pull request in a repository.")]
        public async Task<GitPullRequest> CreatePullRequest(
            [Description("The ID of the repository where the pull request will be created.")] string repositoryId,
            [Description("The name of the source branch.")] string sourceRefName,
            [Description("The name of the target branch.")] string targetRefName,
            [Description("The title of the pull request.")] string title,
            [Description("The description of the pull request.")] string? description = null,
            [Description("Whether the pull request is a draft.")] bool isDraft = false)
        {
            var client = await _adoService.GetGitApiAsync();
            var pr = new GitPullRequest
            {
                SourceRefName = sourceRefName,
                TargetRefName = targetRefName,
                Title = title,
                Description = description,
                IsDraft = isDraft
            };

            return await client.CreatePullRequestAsync(pr, repositoryId);
        }

        [McpServerTool(Name = "get_pull_request")]
        [Description("Get details of a specific pull request by ID.")]
        public async Task<GitPullRequest> GetPullRequestById(
            [Description("The ID of the repository.")] string repositoryId,
            [Description("The ID of the pull request.")] int pullRequestId)
        {
            var client = await _adoService.GetGitApiAsync();
            return await client.GetPullRequestAsync(repositoryId, pullRequestId);
        }

        [McpServerTool(Name = "create_branch")]
        [Description("Create a new branch in a repository from a source branch or commit.")]
        public async Task<string> CreateBranch(
            [Description("The ID of the repository where the branch will be created.")] string repositoryId,
            [Description("The name of the new branch to create.")] string branchName,
            [Description("The name of the source branch to create the new branch from. Defaults to 'main'.")] string sourceBranchName = "main",
            [Description("The commit ID to create the branch from. If not provided, uses the latest commit of the source branch.")] string? sourceCommitId = null)
        {
            var client = await _adoService.GetGitApiAsync();

            string commitId = sourceCommitId ?? string.Empty;
            if (string.IsNullOrEmpty(commitId))
            {
                var refs = await client.GetRefsAsync(repositoryId, filter: $"heads/{sourceBranchName}");
                var sourceRef = refs.FirstOrDefault();
                if (sourceRef == null)
                {
                    throw new ArgumentException($"Source branch '{sourceBranchName}' not found.");
                }
                commitId = sourceRef.ObjectId;
            }

            var refUpdate = new GitRefUpdate
            {
                Name = $"refs/heads/{branchName}",
                OldObjectId = "0000000000000000000000000000000000000000",
                NewObjectId = commitId
            };

            var updateResult = await client.UpdateRefsAsync(new[] { refUpdate }, repositoryId);

            if (updateResult.Any(r => r.Success))
            {
                return $"Branch '{branchName}' created successfully.";
            }
            else
            {
                throw new Exception($"Error creating branch: {updateResult.First().CustomMessage}");
            }
        }
    }
}
