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
    public DateTimeOffset? LastClickUpSyncAt { get; set; }
    public string? LastClickUpSyncSummary { get; set; }
    public string UiPreferences { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Client
{
    public Guid Id { get; set; }
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? OriginalName { get; set; }
    public string? ClickUpFolderId { get; set; }
    public string? ClickUpListId { get; set; }
    public string? Description { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    public bool Active { get; set; } = true;

    /// <summary>Whether a Billable dropdown custom field is available in this client's ClickUp location.</summary>
    public bool BillFieldAvailable { get; set; }
    public string? BillCustomFieldId { get; set; }
    public string? BillYesOptionId { get; set; }
    public string? BillNoOptionId { get; set; }
    public DateTimeOffset? BillFieldCheckedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Project
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Invoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Preparing;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Sheet-shaped task row: manual billing cols + ClickUp API cols.</summary>
public class WorkTask
{
    public Guid Id { get; set; }
    public int ShortId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ProjectId { get; set; }

    public string? Bill { get; set; }
    public decimal? BillableHours { get; set; }
    public decimal? NonBillableHours { get; set; }
    public string? InvoiceLabel { get; set; }
    public string? Note { get; set; }

    public string? ClickUpUrl { get; set; }
    public string? ClickUpTaskId { get; set; }
    public string? ClickUpParentId { get; set; }
    public string? ClickUpFolderId { get; set; }
    public string? ClickUpFolderName { get; set; }
    public string? ClickUpListId { get; set; }
    public string? ClickUpListName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ClickUpStatus { get; set; }
    public int? ClickUpStatusOrder { get; set; }
    public string? Tags { get; set; }
    public DateTimeOffset? DateCreated { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? DateDone { get; set; }
    public DateTimeOffset? DateClosed { get; set; }
    public long? OrderIndex { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class ClickUpContainer
{
    public Guid Id { get; set; }
    public string ContainerType { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentType { get; set; }
    public string? ParentExternalId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
