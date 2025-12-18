using System.ComponentModel;
using System.Text.RegularExpressions;
using AzureDevOpsMcp.Shared.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using ModelContextProtocol.Server;

namespace AzureDevOpsMcp.Manager.Tools;

public sealed record WorkItemSummary(
    int Id,
    string Url,
    string WorkItemType,
    string Title,
    string State,
    string AssignedTo,
    string AreaPath,
    string IterationPath,
    string Tags,
    string Description
);

public sealed record WorkItemNode(WorkItemSummary Summary, List<WorkItemNode> Children);

[McpServerToolType]
public partial class QueryTools(AzureDevOpsService adoService)
{
    private readonly AzureDevOpsService _ado = adoService;

    private static string Field(WorkItem wi, string name)
    {
        if (wi.Fields != null && wi.Fields.TryGetValue(name, out var v) && v is not null)
        {
            return v.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private string WorkItemUrl(int id)
    {
        var orgUrl = _ado.Connection.Uri.ToString().TrimEnd('/');
        return $"{orgUrl}/_workitems/edit/{id}";
    }

    private WorkItemSummary ToSummary(WorkItem wi)
    {
        var id = wi.Id ?? 0;

        return new WorkItemSummary(
            Id: id,
            Url: id == 0 ? "" : WorkItemUrl(id),
            WorkItemType: Field(wi, "System.WorkItemType"),
            Title: Field(wi, "System.Title"),
            State: Field(wi, "System.State"),
            AssignedTo: Field(wi, "System.AssignedTo"),
            AreaPath: Field(wi, "System.AreaPath"),
            IterationPath: Field(wi, "System.IterationPath"),
            Tags: Field(wi, "System.Tags"),
            Description: Field(wi, "System.Description")
        );
    }

    private static int? ExtractFirstInt(string s)
    {
        var m = FirstIntRegex().Match(s ?? string.Empty);
        if (!m.Success)
        {
            return null;
        }

        return int.TryParse(m.Groups["id"].Value, out var id) ? id : null;
    }

    private static int? TryParseIdFromWorkItemUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // URLs look like: https://dev.azure.com/org/_apis/wit/workItems/12345
        var m = WorkItemUrlIdRegex().Match(url);
        if (!m.Success)
        {
            return null;
        }

        return int.TryParse(m.Groups["id"].Value, out var id) ? id : null;
    }

    private static IEnumerable<int> GetChildIds(WorkItem wi)
    {
        if (wi.Relations == null || wi.Relations.Count == 0)
        {
            yield break;
        }

        foreach (var rel in wi.Relations)
        {
            // Parent -> Child link is "Hierarchy-Forward"
            if (
                !string.Equals(
                    rel.Rel,
                    "System.LinkTypes.Hierarchy-Forward",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }

            var childId = TryParseIdFromWorkItemUrl(rel.Url);
            if (childId.HasValue)
            {
                yield return childId.Value;
            }
        }
    }

    private async Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        string project,
        IEnumerable<int> ids,
        WorkItemExpand expand
    )
    {
        var idArray = ids.Distinct().ToArray();
        if (idArray.Length == 0)
        {
            return Array.Empty<WorkItem>();
        }

        var client = await _ado.GetWorkItemTrackingApiAsync();
        var items = await client.GetWorkItemsAsync(project, idArray, expand: expand);
        return items;
    }

    private async Task<(
        WorkItemNode Root,
        List<WorkItemSummary> AllSummaries
    )> BuildDescendantTreeAsync(string project, int rootId, int maxDepth, int maxItems)
    {
        // BFS collecting nodes + adjacency (parent -> children)
        var visited = new HashSet<int>();
        var depthById = new Dictionary<int, int>();
        var childrenByParent = new Dictionary<int, List<int>>();
        var summaryById = new Dictionary<int, WorkItemSummary>();

        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        depthById[rootId] = 0;

        while (queue.Count > 0)
        {
            if (visited.Count >= maxItems)
            {
                break;
            }

            // batch pop to reduce API calls
            var batch = new List<int>(capacity: 200);

            while (queue.Count > 0 && batch.Count < 200)
            {
                var id = queue.Dequeue();
                if (visited.Contains(id))
                {
                    continue;
                }

                var depth = depthById.TryGetValue(id, out var d) ? d : 0;
                if (depth > maxDepth)
                {
                    continue;
                }

                batch.Add(id);
                visited.Add(id);
            }

            if (batch.Count == 0)
            {
                continue;
            }

            // Must include Relations to traverse hierarchy
            var items = await GetWorkItemsAsync(project, batch, WorkItemExpand.Relations);

            foreach (var wi in items)
            {
                if (wi.Id is null || wi.Id.Value == 0)
                {
                    continue;
                }

                var id = wi.Id.Value;
                summaryById[id] = ToSummary(wi);

                var childIds = GetChildIds(wi).Distinct().ToList();
                if (!childrenByParent.TryGetValue(id, out var list))
                {
                    list = new List<int>();
                    childrenByParent[id] = list;
                }

                foreach (var childId in childIds)
                {
                    if (!list.Contains(childId))
                    {
                        list.Add(childId);
                    }

                    if (!depthById.ContainsKey(childId))
                    {
                        depthById[childId] = (depthById.TryGetValue(id, out var pd) ? pd : 0) + 1;
                    }

                    if (!visited.Contains(childId))
                    {
                        queue.Enqueue(childId);
                    }
                }
            }
        }

        // Build tree from adjacency + summaries (ensure every referenced id has a summary)
        // Fetch any missing summaries (e.g., if we hit maxItems mid-level)
        var missing = visited.Where(id => !summaryById.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            var extra = await GetWorkItemsAsync(project, missing, WorkItemExpand.Fields);
            foreach (var wi in extra)
            {
                if (wi.Id is null || wi.Id.Value == 0)
                {
                    continue;
                }

                summaryById[wi.Id.Value] = ToSummary(wi);
            }
        }

        WorkItemNode Build(int id)
        {
            summaryById.TryGetValue(id, out var s);
            s ??= new WorkItemSummary(id, WorkItemUrl(id), "", "", "", "", "", "", "", "");

            var kids = childrenByParent.TryGetValue(id, out var childList)
                ? childList
                : new List<int>();
            var nodes = new List<WorkItemNode>();

            foreach (var childId in kids)
            {
                var childDepth = depthById.TryGetValue(childId, out var d) ? d : 0;
                if (childDepth > maxDepth)
                {
                    continue;
                }

                nodes.Add(Build(childId));
            }

            return new WorkItemNode(s, nodes);
        }

        var root = Build(rootId);
        var all = visited
            .Select(id => summaryById.TryGetValue(id, out var s) ? s : null)
            .Where(s => s is not null)
            .Select(s => s!)
            .OrderBy(s => s.Id)
            .ToList();

        return (root, all);
    }

    [McpServerTool(Name = "get_work_item_summary")]
    [Description(
        "Given a reference like 'feature 43630' or '43630', return work item info. Optionally includes full descendant tree."
    )]
    public async Task<object> GetWorkItemSummary(
        [Description("Reference text like 'feature 43630', 'bug 123', or just '43630'.")]
            string reference,
        [Description("The project name or ID.")] string project,
        [Description("Include all children / grandchildren / etc via hierarchy links.")]
            bool includeDescendants = true,
        [Description("Maximum depth to traverse (0 = just the item).")] int maxDepth = 25,
        [Description("Safety cap: maximum total items returned (root + descendants).")]
            int maxItems = 500
    )
    {
        var id = ExtractFirstInt(reference);

        if (id.HasValue)
        {
            if (!includeDescendants)
            {
                var items = await GetWorkItemsAsync(
                    project,
                    new[] { id.Value },
                    WorkItemExpand.Fields
                );
                var wi = items.FirstOrDefault();
                if (wi is null)
                {
                    return new
                    {
                        found = false,
                        reason = "No work item found for parsed ID.",
                        parsedId = id.Value,
                    };
                }

                return new
                {
                    found = true,
                    parsedId = id.Value,
                    summary = ToSummary(wi),
                };
            }

            var (root, all) = await BuildDescendantTreeAsync(project, id.Value, maxDepth, maxItems);

            return new
            {
                found = true,
                parsedId = id.Value,
                root,
                allItems = all,
                limits = new
                {
                    maxDepth,
                    maxItems,
                    returned = all.Count,
                },
            };
        }

        // If no numeric ID detected, return candidates (so the agent can choose)
        var candidates = (await SearchWorkItems(reference, project, top: 10))
            .Select(w => new
            {
                id = w.Id ?? 0,
                title = Field(w, "System.Title"),
                type = Field(w, "System.WorkItemType"),
                state = Field(w, "System.State"),
                url = w.Id is int wid ? WorkItemUrl(wid) : "",
            })
            .Where(x => x.id != 0)
            .ToArray();

        return new
        {
            found = false,
            reason = "No numeric ID detected in reference; returning candidate matches.",
            candidates,
        };
    }

    // You already have this in your class; leaving signature here so the snippet compiles.
    // Keep your existing implementation.
    public async Task<IEnumerable<WorkItem>> SearchWorkItems(
        string searchText,
        string project,
        string workItemType = "",
        string state = "",
        string assignedTo = "",
        int top = 50
    )
    {
        // your existing SearchWorkItems body
        throw new NotImplementedException();
    }

    [GeneratedRegex(@"(?<id>\d{1,10})", RegexOptions.Compiled)]
    private static partial Regex FirstIntRegex();

    [GeneratedRegex(@"/workItems/(?<id>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WorkItemUrlIdRegex();
}