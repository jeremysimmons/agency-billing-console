namespace Aib.Application;

/// <summary>
/// ClickUp connection configuration. The API token is a secret and is expected to arrive
/// from configuration/environment (e.g. CLICKUP_API_TOKEN), never persisted in the database.
/// </summary>
public sealed class ClickUpOptions
{
    public const string SectionName = "ClickUp";

    public string ApiBaseUrl { get; set; } = "https://api.clickup.com/api/v2/";
    public string? ApiToken { get; set; }

    /// <summary>ClickUp API v2 team id (workspace).</summary>
    public string? TeamId { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(TeamId);
}
