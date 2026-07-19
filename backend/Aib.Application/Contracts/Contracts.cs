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

// ---- Clients ----
public sealed record CreateClientRequest(string Name, string? Code, string? Description, ClientStatus? Status);
public sealed record UpdateClientRequest(string Name, string? Code, string? Description, ClientStatus Status, bool Active);
public sealed record ClientDto(Guid Id, string Name, string? Code, string? Description, ClientStatus Status, bool Active);

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
