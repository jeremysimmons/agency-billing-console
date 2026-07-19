namespace Aib.Domain.Entities;

/// <summary>Maps a ClickUp container (folder/list/…) to an internal client and/or project.</summary>
public class ExternalContainerMapping
{
    public Guid Id { get; set; }
    public Guid ExternalContainerId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public MappingStatus MappingStatus { get; set; } = MappingStatus.Unmapped;
    public MappingSource MappingSource { get; set; } = MappingSource.Manual;
    public Guid? MappedByUserId { get; set; }
    public DateTimeOffset? MappedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Maps a ClickUp work item to an internal task.</summary>
public class ExternalTaskMapping
{
    public Guid Id { get; set; }
    public Guid ExternalWorkItemId { get; set; }
    public Guid? TaskId { get; set; }
    public MappingStatus MappingStatus { get; set; } = MappingStatus.Unmapped;
    public MappingSource MappingSource { get; set; } = MappingSource.Manual;
    public Guid? MappedByUserId { get; set; }
    public DateTimeOffset? MappedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Maps a ClickUp status string to an internal <see cref="WorkStatus"/>.</summary>
public class ExternalStatusMapping
{
    public Guid Id { get; set; }
    public Guid ExternalConnectionId { get; set; }
    public string ExternalStatusName { get; set; } = string.Empty;
    public string? ExternalStatusType { get; set; }
    public WorkStatus InternalStatus { get; set; } = WorkStatus.Pending;
    public bool TreatedAsCompleted { get; set; }
    public bool TreatedAsBillable { get; set; } = true;
    public bool Active { get; set; } = true;
}
