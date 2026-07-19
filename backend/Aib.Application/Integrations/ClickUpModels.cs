namespace Aib.Application.Integrations;

/// <summary>Normalized ClickUp user (assignee/creator).</summary>
public sealed record ClickUpUser(string Id, string? Username, string? Email);

/// <summary>Normalized ClickUp task/subtask as returned by the team task query.</summary>
public sealed record ClickUpTask
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? StatusName { get; init; }
    public string? StatusType { get; init; }
    public bool IsClosed { get; init; }
    public bool Archived { get; init; }
    public string? ParentId { get; init; }

    public string? ListId { get; init; }
    public string? ListName { get; init; }
    public string? FolderId { get; init; }
    public string? FolderName { get; init; }
    public bool FolderHidden { get; init; }
    public string? SpaceId { get; init; }

    public string? AssigneeExternalUserId { get; init; }
    public IReadOnlyList<ClickUpUser> Assignees { get; init; } = [];
    public ClickUpUser? Creator { get; init; }

    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? SourceCreatedAt { get; init; }
    public DateTimeOffset? SourceUpdatedAt { get; init; }

    public int? TimeEstimateMinutes { get; init; }
    public int? TimeSpentMinutes { get; init; }
    public string? Url { get; init; }
    public string RawJson { get; init; } = "{}";
}

/// <summary>One page of the ClickUp team task query.</summary>
public sealed record ClickUpTaskPage(IReadOnlyList<ClickUpTask> Tasks, bool LastPage);

/// <summary>Normalized ClickUp time entry.</summary>
public sealed record ClickUpTimeEntry
{
    public required string Id { get; init; }
    public string? TaskId { get; init; }
    public string ExternalUserId { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public DateOnly WorkDate { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public string? Description { get; init; }
    public bool? Billable { get; init; }
    public DateTimeOffset? SourceCreatedAt { get; init; }
    public DateTimeOffset? SourceUpdatedAt { get; init; }
    public string RawJson { get; init; } = "{}";
}
