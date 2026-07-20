namespace Aib.Domain.Entities;

/// <summary>Internal billable/non-billable time against a task.</summary>
public class TimeEntry
{
    public Guid Id { get; set; }
    public Guid ContractorId { get; set; }
    public Guid TaskId { get; set; }
    public Guid? BillingPeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public bool Billable { get; set; } = true;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public decimal? HourlyRate { get; set; }
    public decimal? BillingAmount { get; set; }
    public Guid? InvoiceLineId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Links an internal time entry to its ClickUp source (idempotent import).</summary>
public class TimeEntrySource
{
    public Guid Id { get; set; }
    public Guid TimeEntryId { get; set; }
    public Guid ExternalTimeEntryId { get; set; }
    public int ImportedDurationMinutes { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
