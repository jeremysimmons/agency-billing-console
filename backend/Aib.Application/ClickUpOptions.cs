namespace Aib.Application;

public sealed class ClickUpOptions
{
    public const string SectionName = "ClickUp";

    public string ApiBaseUrl { get; set; } = "https://api.clickup.com/api/v2/";
    public string? ApiToken { get; set; }
    public string? TeamId { get; set; }
    public string? AssigneeId { get; set; }
    public long InitialCreatedAfterMs { get; set; } = 1735689600000;
    public int PageLimit { get; set; } = 100;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(TeamId);
}
