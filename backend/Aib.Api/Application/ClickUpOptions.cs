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

    /// <summary>ClickUp drop_down custom field mapped to local task.bill ("Billable").</summary>
    public string BillCustomFieldId { get; set; } = "259cf89b-dbc6-45e8-b804-b5c4c8804f74";
    public string BillYesOptionId { get; set; } = "b807de7a-0d4d-4805-a5e3-e3e3935f9ccf";
    public string BillNoOptionId { get; set; } = "4ae1cef5-e93d-411b-bcd3-9115f86a5260";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken) && !string.IsNullOrWhiteSpace(TeamId);
}
