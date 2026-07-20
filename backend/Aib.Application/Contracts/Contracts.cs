using Aib.Domain;

namespace Aib.Application.Contracts;

// ---- Auth ----
public sealed record LoginRequest(string UsernameOrEmail, string Password);
public sealed record MagicLinkRequest(string Email);
public sealed record GoogleLinkRequest(string IdToken);

public sealed record AuthenticatedUser(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool IsContractorSide);

/// <summary>Result of an authentication attempt; carries the session token when successful.</summary>
public sealed record AuthResult(bool Succeeded, AuthenticatedUser? User, string? SessionToken, DateTimeOffset? ExpiresAt, string? Error)
{
    public static AuthResult Ok(AuthenticatedUser user, string token, DateTimeOffset expiresAt) =>
        new(true, user, token, expiresAt, null);
    public static AuthResult Fail(string error) => new(false, null, null, null, error);
}

// ---- Agency ----
public sealed record UpdateAgencyRequest(
    string Name, string? BillingEmail, string? BillingAddress, string Currency, int PaymentTermsDays, bool Active);
public sealed record AgencyDto(
    Guid Id, string Name, string? BillingEmail, string? BillingAddress, string Currency, int PaymentTermsDays, bool Active);

// ---- Clients ----
public sealed record CreateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus? Status);
public sealed record UpdateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus Status, bool Active);
public sealed record ClientDto(Guid Id, string Name, string? Code, string? OriginalName, string? Description, ClientStatus Status, bool Active);
public sealed record DeleteAllClientsResult(int Deleted);

// ---- Projects ----
public sealed record CreateProjectRequest(
    Guid ClientId, string Name, string? Code, string? Description,
    ProjectStatus? Status, BillingType? BillingType,
    decimal? HourlyRate, decimal? FixedFee, int? BudgetMinutes, decimal? BudgetAmount,
    DateOnly? StartDate, DateOnly? EndDate);

public sealed record UpdateProjectRequest(
    string Name, string? Code, string? Description,
    ProjectStatus Status, BillingType BillingType,
    decimal? HourlyRate, decimal? FixedFee, int? BudgetMinutes, decimal? BudgetAmount,
    DateOnly? StartDate, DateOnly? EndDate, bool Active);

public sealed record ProjectDto(
    Guid Id, Guid ClientId, string Name, string? Code, string? Description,
    ProjectStatus Status, BillingType BillingType, decimal? HourlyRate, decimal? FixedFee,
    int? BudgetMinutes, decimal? BudgetAmount, DateOnly? StartDate, DateOnly? EndDate, bool Active);

// ---- Tasks ----
public sealed record CreateTaskRequest(
    Guid ClientId, Guid? ProjectId, Guid? ParentTaskId,
    string Title, string? Description,
    BillingType? BillingType, bool? Billable, decimal? HourlyRate, decimal? FixedFee,
    int? EstimatedMinutes, RollupMode? EstimateRollupMode, RollupMode? ActualRollupMode,
    BillingRollupMode? BillingRollupMode, DateOnly? DueDate);

public sealed record UpdateTaskRequest(
    Guid? ProjectId, Guid? ParentTaskId,
    string Title, string? Description,
    WorkStatus WorkStatus, BillingType BillingType, bool Billable,
    decimal? HourlyRate, decimal? FixedFee,
    int? EstimatedMinutes, RollupMode EstimateRollupMode, RollupMode ActualRollupMode,
    BillingRollupMode BillingRollupMode, DateOnly? DueDate, int SortOrder);

public sealed record TaskDto(
    Guid Id, Guid ClientId, Guid? ProjectId, Guid? ParentTaskId,
    string Title, string? Description,
    WorkStatus WorkStatus, BillingStatus BillingStatus, BillingType BillingType, bool Billable,
    decimal? HourlyRate, decimal? FixedFee, int? EstimatedMinutes,
    RollupMode EstimateRollupMode, RollupMode ActualRollupMode, BillingRollupMode BillingRollupMode,
    DateOnly? DueDate, DateTimeOffset? CompletedAt, DateTimeOffset? FinalizedAt, int SortOrder);

// ---- ClickUp integration ----
public sealed record ExternalConnectionDto(
    Guid Id, string ProviderType, string Name, string? ExternalWorkspaceId,
    ExternalConnectionStatus Status, DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptedSyncAt);

public sealed record TriggerImportRequest(Guid? ConnectionId, bool FullResync);

public sealed record ImportRunDto(
    Guid Id, Guid ExternalConnectionId, ImportType ImportType, ImportStatus Status,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset? SourceUpdatedAfter,
    int RecordsFetched, int RecordsCreated, int RecordsUpdated, int RecordsUnchanged, int RecordsFailed,
    string? ErrorSummary);

// ---- Mapping ----
public sealed record UnmappedContainerDto(
    Guid ContainerId, string ExternalId, ContainerType ContainerType, string Name, string? Url,
    string? ParentExternalId, string? ParentName, ContainerType? ParentContainerType,
    Guid? MappingId, MappingStatus? MappingStatus,
    Guid? SuggestedClientId, string? SuggestedClientName,
    Guid? SuggestedProjectId, string? SuggestedProjectName);

public sealed record UnmappedWorkItemDto(
    Guid WorkItemId, string ExternalId, string Name, string? StatusName, string? Url,
    string? ParentExternalId, Guid? ContainerId, string? ContainerName,
    Guid? MappingId, MappingStatus? MappingStatus,
    Guid? SuggestedTaskId, string? SuggestedTaskTitle,
    Guid? SuggestedClientId, Guid? SuggestedProjectId);

public sealed record ContainerMappingDto(
    Guid Id, Guid ExternalContainerId, string ContainerName, ContainerType ContainerType,
    Guid? ClientId, string? ClientName, Guid? ProjectId, string? ProjectName,
    MappingStatus MappingStatus, MappingSource MappingSource, string? Notes, DateTimeOffset? MappedAt);

public sealed record TaskMappingDto(
    Guid Id, Guid ExternalWorkItemId, string WorkItemName, string? Url,
    Guid? TaskId, string? TaskTitle, MappingStatus MappingStatus, MappingSource MappingSource,
    string? Notes, DateTimeOffset? MappedAt);

public sealed record StatusMappingDto(
    Guid Id, string ExternalStatusName, string? ExternalStatusType,
    WorkStatus InternalStatus, bool TreatedAsCompleted, bool TreatedAsBillable, bool Active);

public sealed record ConfirmContainerMappingRequest(
    Guid? ClientId, Guid? ProjectId, bool CreateClient, bool CreateProject, string? Notes);

public sealed record ConfirmTaskMappingRequest(
    Guid? TaskId, bool CreateTask, string? Notes);

public sealed record IgnoreMappingRequest(string? Notes);

public sealed record UpsertStatusMappingRequest(
    string ExternalStatusName, string? ExternalStatusType,
    WorkStatus InternalStatus, bool TreatedAsCompleted, bool TreatedAsBillable, bool Active);

public sealed record SuggestMappingsResult(int ContainerSuggestions, int TaskSuggestions, int StatusSeeded);
public sealed record ImportFoldersAsClientsResult(int Created, int Skipped);
public sealed record ImportListsAsProjectsResult(int Created, int Skipped);

public sealed record ApplyMappedStatusesResult(int Updated);

// ---- Time & rollups ----
public sealed record CreateTimeEntryRequest(
    Guid TaskId, DateOnly WorkDate, int DurationMinutes,
    string? Description, bool? Billable, decimal? HourlyRate,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

public sealed record UpdateTimeEntryRequest(
    DateOnly WorkDate, int DurationMinutes, string? Description, bool Billable,
    decimal? HourlyRate, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt);

public sealed record TimeEntryDto(
    Guid Id, Guid TaskId, Guid ContractorId, DateOnly WorkDate, int DurationMinutes,
    string? Description, bool Billable, ApprovalStatus ApprovalStatus,
    decimal? HourlyRate, decimal? BillingAmount,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, bool FromImport);

public sealed record TaskRollupDto(
    Guid TaskId, string Title, RollupMode EstimateRollupMode, RollupMode ActualRollupMode,
    int? DirectEstimateMinutes, int RolledUpEstimateMinutes,
    int DirectActualMinutes, int RolledUpActualMinutes,
    int DescendantCount);

public sealed record WorkItemReviewDto(
    Guid TaskId, Guid ClientId, string ClientName, Guid? ProjectId, string? ProjectName,
    string Title, WorkStatus WorkStatus, BillingStatus BillingStatus,
    DateTimeOffset? CompletedAt, int? EstimatedMinutes,
    int ActualMinutes, int BillableMinutes, decimal? BillingAmountEstimate,
    string? ClickUpUrl, string? ClickUpStatus);

public sealed record FinalizeWorkRequest(string? Notes);
public sealed record ExcludeWorkRequest(string? Reason);

public sealed record SyncImportedTimeResult(int Linked, int Skipped, int Failed);
