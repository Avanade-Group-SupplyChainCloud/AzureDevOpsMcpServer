using System.ComponentModel;
using System.Text.Json;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.Wiki.WebApi;
using Microsoft.TeamFoundation.Wiki.WebApi.Contracts;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

[McpServerToolType]
public class WikiTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _adoService = adoService;

    [McpServerTool(Name = "list_wikis")]
    [Description("List all wikis.")]
    public async Task<IEnumerable<WikiV2>> ListWikis()
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        var wikis = await client.GetAllWikisAsync(project);
        return wikis ?? Enumerable.Empty<WikiV2>();
    }

    [McpServerTool(Name = "get_wiki")]
    [Description("Get details of a specific wiki by identifier.")]
    public async Task<WikiV2> GetWiki(
        [Description("The wiki identifier or name.")] string wikiIdentifier
    )
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        return await client.GetWikiAsync(project, wikiIdentifier);
    }

    [McpServerTool(Name = "list_wiki_pages")]
    [Description("List pages in a wiki with optional pagination.")]
    public async Task<string> ListWikiPages(
        [Description("The wiki identifier or name.")] string wikiIdentifier,
        [Description("Maximum number of pages to return.")] int top = 100,
        [Description("Number of days to include page views for.")] int? pageViewsForDays = null
    )
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        var request = new WikiPagesBatchRequest
        {
            Top = top,
            PageViewsForDays = pageViewsForDays
        };
        var pages = await client.GetPagesBatchAsync(request, project, wikiIdentifier);
        return JsonSerializer.Serialize(pages, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_wiki_page")]
    [Description("Get wiki page metadata (without content).")]
    public async Task<WikiPageResponse> GetWikiPage(
        [Description("The wiki identifier or name.")] string wikiIdentifier,
        [Description("The path of the wiki page.")] string path,
        [Description("The recursion level for subpages: 'none', 'oneLevel', 'full'.")]
            string recursionLevel = "none"
    )
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        // Use the overload that accepts string for recursion
        return await client.GetPageAsync(project, wikiIdentifier, path);
    }

    [McpServerTool(Name = "get_wiki_page_content")]
    [Description("Get wiki page content as markdown.")]
    public async Task<string> GetWikiPageContent(
        [Description("The wiki identifier or name.")] string wikiIdentifier,
        [Description("The path of the wiki page.")] string path
    )
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        var page = await client.GetPageAsync(project, wikiIdentifier, path, includeContent: true);
        return page?.Page?.Content ?? string.Empty;
    }

    [McpServerTool(Name = "create_or_update_wiki_page")]
    [Description("Create a new wiki page or update an existing one.")]
    public async Task<WikiPageResponse> CreateOrUpdatePage(
        [Description("The wiki identifier or name.")] string wikiIdentifier,
        [Description("The path of the wiki page.")] string path,
        [Description("The content of the wiki page.")] string content,
        [Description("A comment for the update.")] string comment = null
    )
    {
        var client = await _adoService.GetWikiApiAsync();
        var project = _adoService.DefaultProject;
        var pageUpdate = new WikiPageCreateOrUpdateParameters { Content = content };

        return await client.CreateOrUpdatePageAsync(
            pageUpdate,
            project,
            wikiIdentifier,
            path,
            comment
        );
    }
}