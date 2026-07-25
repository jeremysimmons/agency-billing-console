namespace Aib.Application.Integrations;

public sealed record ClickUpUser(string Id, string? Username, string? Email);

public sealed record ClickUpTask
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? StatusName { get; init; }
    public int? StatusOrderIndex { get; init; }
    public string? ParentId { get; init; }
    public string? ListId { get; init; }
    public string? ListName { get; init; }
    public string? FolderId { get; init; }
    public string? FolderName { get; init; }
    public bool FolderHidden { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public DateTimeOffset? SourceCreatedAt { get; init; }
    public long? OrderIndex { get; init; }
    public decimal? EstimatedHours { get; init; }
    public decimal? ActualHours { get; init; }
    public string? Url { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<ClickUpTaskCustomField> CustomFields { get; init; } = [];
}

public sealed record ClickUpTaskPage(IReadOnlyList<ClickUpTask> Tasks, bool LastPage);

public sealed record ClickUpCustomFieldOption(string Id, string Name, int? OrderIndex = null);

public sealed record ClickUpCustomField(
    string Id,
    string Name,
    string Type,
    IReadOnlyList<ClickUpCustomFieldOption> Options);

/// <summary>Custom field value attached to a ClickUp task (includes dropdown options for resolution).</summary>
public sealed record ClickUpTaskCustomField(
    string Id,
    string Name,
    string? Type,
    /// <summary>Raw dropdown value: option orderindex (number) or option id (string), or null if unset.</summary>
    string? Value,
    IReadOnlyList<ClickUpCustomFieldOption> Options);

public interface IClickUpClient
{
    Task<ClickUpTaskPage> GetTasksAsync(
        string teamId, string? assigneeExternalUserId, int page, CancellationToken ct = default);

    Task<ClickUpTask> GetTaskAsync(string taskId, CancellationToken ct = default);

    Task SetTaskCustomFieldAsync(string taskId, string fieldId, object? value, CancellationToken ct = default);

    Task<IReadOnlyList<ClickUpCustomField>> GetListCustomFieldsAsync(string listId, CancellationToken ct = default);

    Task<IReadOnlyList<ClickUpCustomField>> GetFolderCustomFieldsAsync(string folderId, CancellationToken ct = default);

    Task<IReadOnlyList<ClickUpCustomField>> GetSpaceCustomFieldsAsync(string spaceId, CancellationToken ct = default);

    Task<decimal> GetTaskTimeSpentHoursAsync(string taskId, CancellationToken ct = default);

    Task CreateTimeEntryAsync(
        string teamId,
        string taskId,
        long startMs,
        long durationMs,
        long assigneeId,
        bool billable,
        string description,
        CancellationToken ct = default);
}
