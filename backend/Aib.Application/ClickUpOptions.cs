namespace Aib.Application;

/// <summary>
/// ClickUp connection configuration. The API token is a secret and is expected to arrive
/// from configuration/environment (e.g. CLICKUP_API_TOKEN), never persisted in the database.
/// </summary>
public sealed class ClickUpOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.clickup.com/api/v2/";
    public string? ApiToken { get; set; }
    public string? TeamId { get; set; }

    /// <summary>Optional contractor ClickUp user id used to scope the task query.</summary>
    public string? AssigneeId { get; set; }

    /// <summary>Milliseconds epoch used as the earliest window for the first (full) import.</summary>
    public long InitialCreatedAfterMs { get; set; } = 1735689600000; // Jan 1, 2025 UTC

    public int PageLimit { get; set; } = 100;

    /// <summary>Quartz cron for scheduled incremental imports. Default: every 30 minutes.</summary>
    public string ImportCron { get; set; } = "0 0/30 * * * ?";

    /// <summary>Enable the scheduled background import.</summary>
    public bool ScheduleEnabled { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(TeamId);
}
