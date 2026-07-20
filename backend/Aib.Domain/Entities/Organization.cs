namespace Aib.Domain.Entities;

public class Agency
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string? BillingAddress { get; set; }
    public string Currency { get; set; } = "USD";
    public int PaymentTermsDays { get; set; } = 30;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Contractor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal? DefaultHourlyRate { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Client
{
    public Guid Id { get; set; }
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    /// <summary>Source title before Code/Name parsing (e.g. ClickUp folder name).</summary>
    public string? OriginalName { get; set; }
    public string? Description { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Project
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public BillingType BillingType { get; set; } = BillingType.Hourly;
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFee { get; set; }
    public int? BudgetMinutes { get; set; }
    public decimal? BudgetAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkTask
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkStatus WorkStatus { get; set; } = WorkStatus.Pending;
    public BillingStatus BillingStatus { get; set; } = BillingStatus.NotReady;
    public BillingType BillingType { get; set; } = BillingType.Hourly;
    public bool Billable { get; set; } = true;
    public decimal? HourlyRate { get; set; }
    public decimal? FixedFee { get; set; }
    public int? EstimatedMinutes { get; set; }
    public RollupMode EstimateRollupMode { get; set; } = RollupMode.Direct;
    public RollupMode ActualRollupMode { get; set; } = RollupMode.DirectAndChildren;
    public BillingRollupMode BillingRollupMode { get; set; } = BillingRollupMode.Task;
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
