using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Aib.Application;
using Aib.Application.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Infrastructure.Integrations;

public sealed class ClickUpClient(HttpClient http, IOptions<ClickUpOptions> options, ILogger<ClickUpClient> logger)
    : IClickUpClient
{
    private readonly ClickUpOptions _options = options.Value;

    public async Task<ClickUpTaskPage> GetTasksAsync(
        string teamId, string? assigneeExternalUserId, int page, CancellationToken ct = default)
    {
        var query = $"team/{Uri.EscapeDataString(teamId)}/task" +
                    "?reverse=true&include_closed=true&subtasks=true" +
                    $"&page={page}&limit={_options.PageLimit}" +
                    $"&date_created_gt={_options.InitialCreatedAfterMs}";

        if (!string.IsNullOrWhiteSpace(assigneeExternalUserId))
            query += $"&assignees[]={Uri.EscapeDataString(assigneeExternalUserId)}";

        using var doc = await GetJsonAsync(query, ct);
        var root = doc.RootElement;

        var tasks = new List<ClickUpTask>();
        if (root.TryGetProperty("tasks", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                tasks.Add(ParseTask(el));

        var lastPage = root.TryGetProperty("last_page", out var lp) && lp.ValueKind == JsonValueKind.True;
        return new ClickUpTaskPage(tasks, lastPage || tasks.Count == 0);
    }

    public async Task SetTaskCustomFieldAsync(string taskId, string fieldId, object? value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new { value });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        await SendAsync(HttpMethod.Post, $"task/{Uri.EscapeDataString(taskId)}/field/{Uri.EscapeDataString(fieldId)}", content, ct);
    }

    public async Task<decimal> GetTaskTimeSpentHoursAsync(string taskId, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"task/{Uri.EscapeDataString(taskId)}", ct);
        var ms = GetLong(doc.RootElement, "time_spent") ?? 0;
        return ms > 0 ? Math.Round(ms / 3_600_000m, 2) : 0;
    }

    public async Task CreateTimeEntryAsync(
        string teamId,
        string taskId,
        long startMs,
        long durationMs,
        long assigneeId,
        bool billable,
        string description,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            tid = taskId,
            start = startMs,
            duration = durationMs,
            assignee = assigneeId,
            billable,
            description,
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        await SendAsync(HttpMethod.Post, $"team/{Uri.EscapeDataString(teamId)}/time_entries", content, ct);
    }

    private async Task SendAsync(HttpMethod method, string relativeUrl, HttpContent? content, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, relativeUrl) { Content = content };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
                return;

            var retryable = response.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

            if (retryable && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(1500 * attempt);
                logger.LogWarning("ClickUp HTTP {Status}; retry {Attempt}/{Max} after {Delay}ms",
                    (int)response.StatusCode, attempt + 1, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                continue;
            }

            throw new InvalidOperationException($"ClickUp API error HTTP {(int)response.StatusCode}: {body}");
        }

        throw new InvalidOperationException("ClickUp request failed after retries.");
    }

    private static ClickUpTask ParseTask(JsonElement el)
    {
        var status = el.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Object ? s : (JsonElement?)null;
        var list = GetObject(el, "list");
        var folder = GetObject(el, "folder");

        var tags = new List<string>();
        if (el.TryGetProperty("tags", out var tagArr) && tagArr.ValueKind == JsonValueKind.Array)
            foreach (var tag in tagArr.EnumerateArray())
            {
                var name = GetString(tag, "name");
                if (!string.IsNullOrWhiteSpace(name))
                    tags.Add(name);
            }

        var folderName = folder is { } f2 ? GetString(f2, "name") : null;
        var folderHidden = folder is { } f3 && GetBool(f3, "hidden");
        var listName = list is { } l2 ? GetString(l2, "name") : null;
        if (folderHidden || string.Equals(folderName, "hidden", StringComparison.OrdinalIgnoreCase))
            folderName = listName;

        return new ClickUpTask
        {
            Id = GetString(el, "id") ?? string.Empty,
            Name = GetString(el, "name"),
            Description = GetString(el, "description") ?? GetString(el, "text_content"),
            StatusName = status is { } st ? GetString(st, "status") : null,
            StatusOrderIndex = status is { } st2 ? GetInt(st2, "orderindex") : null,
            ParentId = GetString(el, "parent"),
            ListId = list is { } l ? GetString(l, "id") : null,
            ListName = listName,
            FolderId = folder is { } f ? GetString(f, "id") : null,
            FolderName = folderName,
            FolderHidden = folderHidden,
            DueDate = MsToDate(GetLong(el, "due_date")),
            CompletedAt = MsToDate(GetLong(el, "date_done")),
            ClosedAt = MsToDate(GetLong(el, "date_closed")),
            SourceCreatedAt = MsToDate(GetLong(el, "date_created")),
            OrderIndex = GetLong(el, "orderindex"),
            EstimatedHours = MsToHours(GetLong(el, "time_estimate")),
            ActualHours = MsToHours(GetLong(el, "time_spent")),
            Url = GetString(el, "url"),
            Tags = tags
        };
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
                catch (JsonException ex) { throw new InvalidOperationException("ClickUp returned invalid JSON.", ex); }
            }

            var retryable = response.StatusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

            if (retryable && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(1500 * attempt);
                logger.LogWarning("ClickUp HTTP {Status}; retry {Attempt}/{Max} after {Delay}ms",
                    (int)response.StatusCode, attempt + 1, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
                continue;
            }

            throw new InvalidOperationException($"ClickUp API error HTTP {(int)response.StatusCode}: {body}");
        }

        throw new InvalidOperationException("ClickUp request failed after retries.");
    }

    private static JsonElement? GetObject(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            _ => v.ToString()
        };
    }

    private static bool GetBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static long? GetLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null
        };
    }

    private static int? GetInt(JsonElement el, string name)
    {
        var n = GetLong(el, name);
        return n is >= int.MinValue and <= int.MaxValue ? (int)n : null;
    }

    private static DateTimeOffset? MsToDate(long? ms) =>
        ms is { } v && v > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(v) : null;

    private static decimal? MsToHours(long? ms) =>
        ms is { } v && v > 0 ? Math.Round(v / 3_600_000m, 2) : null;
}
