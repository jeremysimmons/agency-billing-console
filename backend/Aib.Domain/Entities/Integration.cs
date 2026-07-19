namespace Aib.Domain.Entities;

/// <summary>A configured link to an external work system (ClickUp). Tokens live in a secret store, not here.</summary>
public class ExternalConnection
{
    public Guid Id { get; set; }
    public Guid AgencyId { get; set; }
    public string ProviderType { get; set; } = "clickup";
    public string Name { get; set; } = string.Empty;
    public string? ExternalWorkspaceId { get; set; }
    public string AuthenticationReference { get; set; } = string.Empty;
    public ExternalConnectionStatus Status { get; set; } = ExternalConnectionStatus.Active;
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset? LastAttemptedSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Maps an external (ClickUp) user id to an internal user/contractor.</summary>
public class ExternalIdentity
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ContractorId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? ExternalUsername { get; set; }
    public string? ExternalEmail { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>ClickUp workspace/space/folder/list record (external staging).</summary>
public class ExternalContainer
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public string? ExternalParentId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public ContainerType ContainerType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public string? RawDataJson { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Latest imported state of a ClickUp task or subtask (external staging).</summary>
public class ExternalWorkItem
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public Guid? ExternalContainerId { get; set; }
    public string? ExternalParentWorkItemId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public WorkItemType ItemType { get; set; } = WorkItemType.Task;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StatusName { get; set; }
    public string? StatusType { get; set; }
    public bool IsClosed { get; set; }
    public bool Archived { get; set; }
    public string? AssigneeExternalUserId { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? TimeEstimateMinutes { get; set; }
    public int? TimeSpentMinutes { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? SourceCreatedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAt { get; set; }
    public string? RawDataJson { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
}

/// <summary>A ClickUp time entry (external staging).</summary>
public class ExternalTimeEntry
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public Guid? ExternalWorkItemId { get; set; }
    public string? ExternalWorkItemExternalId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public bool? Billable { get; set; }
    public DateTimeOffset? SourceCreatedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAt { get; set; }
    public string? RawDataJson { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
}

/// <summary>One execution of an import against a connection.</summary>
public class ImportRun
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public ImportType ImportType { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Queued;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAfter { get; set; }
    public int RecordsFetched { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsUnchanged { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorSummary { get; set; }
    public Guid? TriggeredByUserId { get; set; }
}

/// <summary>Per-entity outcome within an import run (diagnostics).</summary>
public class ImportRecord
{
    public Guid Id { get; set; }
    public Guid ImportRunId { get; set; }
    public ExternalEntityType ExternalEntityType { get; set; }
    public string ExternalEntityId { get; set; } = string.Empty;
    public ImportAction Action { get; set; }
    public ImportRecordStatus Status { get; set; }
    public Guid? ExternalRecordId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>Incremental sync watermark per connection + entity type.</summary>
public class SyncCursor
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public ExternalEntityType EntityType { get; set; }
    public string? CursorValue { get; set; }
    public DateTimeOffset? LastSourceUpdatedAt { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
}
