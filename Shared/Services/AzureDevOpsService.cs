using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.Wiki.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using System.Net.Http.Headers;
using System.Text;

namespace AzureDevOpsMcp.Shared.Services;

public class AzureDevOpsSettings
{
    public string OrgUrl { get; set; }
    public string PersonalAccessToken { get; set; }
}

public class AzureDevOpsService(IOptions<AzureDevOpsSettings> settings)
{
    private readonly VssConnection _connection = new(new Uri(settings.Value.OrgUrl), new VssBasicCredential(string.Empty, settings.Value.PersonalAccessToken));
    private readonly string _personalAccessToken = settings.Value.PersonalAccessToken;

    public VssConnection Connection => _connection;
    public string PersonalAccessToken => _personalAccessToken;

        /// <summary>
        /// Creates an HttpClient configured with the PAT for REST API calls.
        /// </summary>
        public HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_personalAccessToken}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        public async Task<ProjectHttpClient> GetCoreApiAsync() => await _connection.GetClientAsync<ProjectHttpClient>();
        public async Task<TeamHttpClient> GetTeamApiAsync() => await _connection.GetClientAsync<TeamHttpClient>();
        public async Task<GitHttpClient> GetGitApiAsync() => await _connection.GetClientAsync<GitHttpClient>();
        public async Task<WorkItemTrackingHttpClient> GetWorkItemTrackingApiAsync() => await _connection.GetClientAsync<WorkItemTrackingHttpClient>();
        
        public async Task<BuildHttpClient> GetBuildApiAsync() => await _connection.GetClientAsync<BuildHttpClient>(); 
        
        // WikiHttpClient
        public async Task<WikiHttpClient> GetWikiApiAsync() => await _connection.GetClientAsync<WikiHttpClient>();
        
        public async Task<WorkHttpClient> GetWorkApiAsync() => await _connection.GetClientAsync<WorkHttpClient>();
        
        public async Task<TestManagementHttpClient> GetTestManagementApiAsync() => await _connection.GetClientAsync<TestManagementHttpClient>();
}
