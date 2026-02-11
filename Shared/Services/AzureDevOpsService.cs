using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.Wiki.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzureDevOpsMcp.Shared.Services;

public class AzureDevOpsSettings
{
    public string OrgUrl { get; set; }
    public string DefaultProject { get; set; }

    // PAT auth
    public string PersonalAccessToken { get; set; }

    // Entra ID app registration (client credentials) auth
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}

public class AzureDevOpsService(IOptions<AzureDevOpsSettings> settings)
{
    private readonly AzureDevOpsSettings _settings = settings.Value;

    private readonly VssConnection _connection = CreateConnection(settings.Value);

    private readonly string _defaultProject = settings.Value.DefaultProject;

    // Kept for Entra token acquisition on HttpClient calls
    private readonly ClientSecretCredential _entraCredential = IsEntraConfigured(settings.Value)
        ? new ClientSecretCredential(settings.Value.TenantId, settings.Value.ClientId, settings.Value.ClientSecret)
        : null;

    public VssConnection Connection => _connection;
    public string DefaultProject => _defaultProject;

    /// <summary>
    /// Returns the provided project if not null/empty, otherwise returns the default project from settings.
    /// </summary>
    public string GetProjectOrDefault(string project) =>
        string.IsNullOrWhiteSpace(project) ? _defaultProject : project;

    /// <summary>
    /// Creates an HttpClient configured for Azure DevOps REST API calls.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();

        if (_entraCredential != null)
        {
            var token = _entraCredential.GetToken(
                new TokenRequestContext(["499b84ac-1321-427f-aa17-267ca6975798/.default"]),
                default);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Token);
        }
        else
        {
            var authValue = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authValue);
        }

        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
        return httpClient;
    }

    private static VssConnection CreateConnection(AzureDevOpsSettings s)
    {
        var orgUri = new Uri(s.OrgUrl);

        if (IsEntraConfigured(s))
        {
            var credential = new ClientSecretCredential(s.TenantId, s.ClientId, s.ClientSecret);
            return new VssConnection(orgUri, new VssAzureIdentityCredential(credential));
        }

        return new VssConnection(orgUri,
            new VssBasicCredential(string.Empty, s.PersonalAccessToken));
    }

    private static bool IsEntraConfigured(AzureDevOpsSettings s) =>
        !string.IsNullOrWhiteSpace(s.TenantId)
        && !string.IsNullOrWhiteSpace(s.ClientId)
        && !string.IsNullOrWhiteSpace(s.ClientSecret);

    public async Task<ProjectHttpClient> GetCoreApiAsync() =>
        await _connection.GetClientAsync<ProjectHttpClient>();

    public async Task<TeamHttpClient> GetTeamApiAsync() =>
        await _connection.GetClientAsync<TeamHttpClient>();

    public async Task<GitHttpClient> GetGitApiAsync() =>
        await _connection.GetClientAsync<GitHttpClient>();

    public async Task<WorkItemTrackingHttpClient> GetWorkItemTrackingApiAsync() =>
        await _connection.GetClientAsync<WorkItemTrackingHttpClient>();

    public async Task<BuildHttpClient> GetBuildApiAsync() =>
        await _connection.GetClientAsync<BuildHttpClient>();

    // WikiHttpClient
    public async Task<WikiHttpClient> GetWikiApiAsync() =>
        await _connection.GetClientAsync<WikiHttpClient>();

    public async Task<WorkHttpClient> GetWorkApiAsync() =>
        await _connection.GetClientAsync<WorkHttpClient>();

    public async Task<TestManagementHttpClient> GetTestManagementApiAsync() =>
        await _connection.GetClientAsync<TestManagementHttpClient>();
}