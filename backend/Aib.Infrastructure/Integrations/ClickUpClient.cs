using System.Globalization;
using System.Net;
using System.Text.Json;
using Aib.Application;
using Aib.Application.Abstractions;
using Aib.Application.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Infrastructure.Integrations;

/// <summary>ClickUp v2 REST client (read-only) with retry on rate-limit / transient errors.</summary>
public sealed class ClickUpClient(HttpClient http, IOptions<ClickUpOptions> options, ILogger<ClickUpClient> logger)
    : IClickUpClient
{
    private readonly ClickUpOptions _options = options.Value;

    public async Task<ClickUpTaskPage> GetTasksAsync(
        string teamId, long? dateUpdatedGtMs, string? assigneeExternalUserId, int page, CancellationToken ct = default)
    {
        var query = $"team/{Uri.EscapeDataString(teamId)}/task" +
                    "?reverse=true&include_closed=true&subtasks=true" +
                    $"&page={page}";

        if (dateUpdatedGtMs is { } updated)
            query += $"&date_updated_gt={updated}";
        else
            query += $"&date_created_gt={_options.InitialCreatedAfterMs}";

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

    public async Task<IReadOnlyList<ClickUpTimeEntry>> GetTimeEntriesAsync(
        string teamId, long? startDateMs, CancellationToken ct = default)
    {
        var query = $"team/{Uri.EscapeDataString(teamId)}/time_entries";
        if (startDateMs is { } start)
            query += $"?start_date={start}";

        using var doc = await GetJsonAsync(query, ct);
        var root = doc.RootElement;

        var entries = new List<ClickUpTimeEntry>();
        if (root.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                entries.Add(ParseTimeEntry(el));

        return entries;
    }

    public async Task<IReadOnlyList<ClickUpSpace>> GetSpacesAsync(string teamId, CancellationToken ct = default)
    {
        var query = $"team/{Uri.EscapeDataString(teamId)}/space?archived=false";
        using var doc = await GetJsonAsync(query, ct);
        var root = doc.RootElement;

        var spaces = new List<ClickUpSpace>();
        if (root.TryGetProperty("spaces", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                var id = GetString(el, "id");
                var name = GetString(el, "name");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    spaces.Add(new ClickUpSpace(id, name));
            }
        }

        return spaces;
    }

    private static ClickUpTask ParseTask(JsonElement el)
    {
        var status = el.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Object ? s : (JsonElement?)null;
        var list = GetObject(el, "list");
        var folder = GetObject(el, "folder");
        var space = GetObject(el, "space");

        var assignees = new List<ClickUpUser>();
        if (el.TryGetProperty("assignees", out var aArr) && aArr.ValueKind == JsonValueKind.Array)
            foreach (var a in aArr.EnumerateArray())
                assignees.Add(ParseUser(a));

        var estimateMs = GetLong(el, "time_estimate");
        var spentMs = GetLong(el, "time_spent");

        return new ClickUpTask
        {
            Id = GetString(el, "id") ?? string.Empty,
            Name = GetString(el, "name"),
            Description = GetString(el, "description") ?? GetString(el, "text_content"),
            StatusName = status is { } st ? GetString(st, "status") : null,
            StatusType = status is { } st2 ? GetString(st2, "type") : null,
            IsClosed = string.Equals(status is { } st3 ? GetString(st3, "type") : null, "closed", StringComparison.OrdinalIgnoreCase),
            Archived = GetBool(el, "archived"),
            ParentId = GetString(el, "parent"),
            ListId = list is { } l ? GetString(l, "id") : null,
            ListName = list is { } l2 ? GetString(l2, "name") : null,
            FolderId = folder is { } f ? GetString(f, "id") : null,
            FolderName = folder is { } f2 ? GetString(f2, "name") : null,
            FolderHidden = folder is { } f3 && GetBool(f3, "hidden"),
            SpaceId = space is { } sp ? GetString(sp, "id") : null,
            SpaceName = space is { } spaceEl ? GetString(spaceEl, "name") : null,
            AssigneeExternalUserId = assignees.Count > 0 ? assignees[0].Id : null,
            Assignees = assignees,
            Creator = el.TryGetProperty("creator", out var cr) && cr.ValueKind == JsonValueKind.Object ? ParseUser(cr) : null,
            StartDate = MsToDate(GetLong(el, "start_date")),
            DueDate = MsToDate(GetLong(el, "due_date")),
            CompletedAt = MsToDate(GetLong(el, "date_done")),
            SourceCreatedAt = MsToDate(GetLong(el, "date_created")),
            SourceUpdatedAt = MsToDate(GetLong(el, "date_updated")),
            TimeEstimateMinutes = estimateMs is { } e ? (int)(e / 60000) : null,
            TimeSpentMinutes = spentMs is { } sp2 && sp2 > 0 ? (int)(sp2 / 60000) : null,
            Url = GetString(el, "url"),
            RawJson = el.GetRawText()
        };
    }

    private static ClickUpTimeEntry ParseTimeEntry(JsonElement el)
    {
        var task = GetObject(el, "task");
        var user = GetObject(el, "user");
        var startMs = GetLong(el, "start");
        var endMs = GetLong(el, "end");
        var durationMs = GetLong(el, "duration") ?? 0;
        var started = MsToDate(startMs);
        var added = MsToDate(GetLong(el, "date_added"));
        var workDate = started ?? added ?? DateTimeOffset.UtcNow;

        return new ClickUpTimeEntry
        {
            Id = GetString(el, "id") ?? string.Empty,
            TaskId = task is { } t ? GetString(t, "id") : null,
            ExternalUserId = user is { } u ? GetString(u, "id") ?? string.Empty : string.Empty,
            DurationMinutes = durationMs > 0 ? (int)(durationMs / 60000) : 0,
            WorkDate = DateOnly.FromDateTime(workDate.UtcDateTime),
            StartedAt = started,
            EndedAt = MsToDate(endMs),
            Description = GetString(el, "description"),
            Billable = el.TryGetProperty("billable", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False ? b.GetBoolean() : null,
            SourceCreatedAt = added,
            RawJson = el.GetRawText()
        };
    }

    private static ClickUpUser ParseUser(JsonElement el) =>
        new(GetString(el, "id") ?? string.Empty, GetString(el, "username"), GetString(el, "email"));

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

        throw new InvalidOperationException($"ClickUp request failed after {maxAttempts} attempts.");
    }

    // ---- JSON helpers (ClickUp encodes numbers as strings) ----

    private static JsonElement? GetObject(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

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

    private static DateTimeOffset? MsToDate(long? ms) =>
        ms is { } v && v > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(v) : null;
}
