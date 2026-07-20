using System.Net;
using System.Text.Json;
using Aib.Application;
using Aib.Application.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Infrastructure.Integrations;

/// <summary>
/// Builds the flat ClickUp hierarchy via API v2 endpoints,
/// emitting v3 type names (workspace → space → folder → list).
/// </summary>
public sealed class ClickUpHierarchyBuilder(
    HttpClient http,
    IOptions<ClickUpOptions> options,
    ILogger<ClickUpHierarchyBuilder> logger) : IClickUpHierarchyBuilder
{
    private readonly ClickUpOptions _options = options.Value;

    public async Task<IReadOnlyList<ClickUpHierarchyNode>> BuildAsync(
        string? teamId = null, CancellationToken ct = default)
    {
        var workspaceId = teamId ?? _options.TeamId
            ?? throw new InvalidOperationException("ClickUp TeamId is not configured.");

        var rows = new List<ClickUpHierarchyNode>();

        foreach (var space in await GetSpacesAsync(workspaceId, ct))
        {
            rows.Add(new ClickUpHierarchyNode(
                ClickUpHierarchyTypes.Space, space.Id, space.Name,
                ClickUpHierarchyTypes.Workspace, workspaceId));

            foreach (var folder in await GetFoldersAsync(space.Id, ct))
            {
                rows.Add(new ClickUpHierarchyNode(
                    ClickUpHierarchyTypes.Folder, folder.Id, folder.Name,
                    ClickUpHierarchyTypes.Space, space.Id));

                foreach (var list in folder.Lists)
                {
                    rows.Add(new ClickUpHierarchyNode(
                        ClickUpHierarchyTypes.List, list.Id, list.Name,
                        ClickUpHierarchyTypes.Folder, folder.Id));
                }
            }

            foreach (var list in await GetFolderlessListsAsync(space.Id, ct))
            {
                rows.Add(new ClickUpHierarchyNode(
                    ClickUpHierarchyTypes.List, list.Id, list.Name,
                    ClickUpHierarchyTypes.Space, space.Id));
            }
        }

        return rows;
    }

    private async Task<IReadOnlyList<NamedNode>> GetSpacesAsync(string teamId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"team/{Uri.EscapeDataString(teamId)}/space?archived=false", ct);
        return ParseNamedArray(doc.RootElement, "spaces");
    }

    private async Task<IReadOnlyList<FolderNode>> GetFoldersAsync(string spaceId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"space/{Uri.EscapeDataString(spaceId)}/folder?archived=false", ct);
        var root = doc.RootElement;
        var folders = new List<FolderNode>();

        if (!root.TryGetProperty("folders", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return folders;

        foreach (var el in arr.EnumerateArray())
        {
            var id = GetString(el, "id");
            var name = GetString(el, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                continue;

            var lists = new List<NamedNode>();
            if (el.TryGetProperty("lists", out var listArr) && listArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var listEl in listArr.EnumerateArray())
                {
                    var listId = GetString(listEl, "id");
                    var listName = GetString(listEl, "name");
                    if (!string.IsNullOrWhiteSpace(listId) && !string.IsNullOrWhiteSpace(listName))
                        lists.Add(new NamedNode(listId, listName));
                }
            }

            folders.Add(new FolderNode(id, name, lists));
        }

        return folders;
    }

    private async Task<IReadOnlyList<NamedNode>> GetFolderlessListsAsync(string spaceId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"space/{Uri.EscapeDataString(spaceId)}/list?archived=false", ct);
        return ParseNamedArray(doc.RootElement, "lists");
    }

    private static IReadOnlyList<NamedNode> ParseNamedArray(JsonElement root, string property)
    {
        var items = new List<NamedNode>();
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return items;

        foreach (var el in arr.EnumerateArray())
        {
            var id = GetString(el, "id");
            var name = GetString(el, "name");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                items.Add(new NamedNode(id, name));
        }

        return items;
    }

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var response = await http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                try { return JsonDocument.Parse(body); }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("ClickUp returned invalid JSON.", ex);
                }
            }

            var retryable = response.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

            if (retryable && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(1500 * attempt);
                logger.LogWarning(
                    "ClickUp HTTP {Status}; retry {Attempt}/{Max} after {Delay}ms",
                    (int)response.StatusCode, attempt + 1, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                continue;
            }

            throw new InvalidOperationException($"ClickUp API error HTTP {(int)response.StatusCode}: {body}");
        }

        throw new InvalidOperationException($"ClickUp request failed after {maxAttempts} attempts.");
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => v.ToString()
        };
    }

    private sealed record NamedNode(string Id, string Name);
    private sealed record FolderNode(string Id, string Name, IReadOnlyList<NamedNode> Lists);
}
