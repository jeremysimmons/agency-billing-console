using Aib.Domain;
using Aib.Domain.Entities;
using Aib.Application.Integrations;

namespace Aib.Application.Abstractions;

/// <summary>Read-only ClickUp API client. Never exposes credentials to callers.</summary>
public interface IClickUpClient
{
    /// <summary>Fetch a page of tasks (subtasks included) for a team, optionally only those updated after a watermark.</summary>
    Task<ClickUpTaskPage> GetTasksAsync(
        string teamId, long? dateUpdatedGtMs, string? assigneeExternalUserId, int page, CancellationToken ct = default);

    /// <summary>Fetch time entries for a team within an optional start window (ms epoch).</summary>
    Task<IReadOnlyList<ClickUpTimeEntry>> GetTimeEntriesAsync(
        string teamId, long? startDateMs, CancellationToken ct = default);
}

/// <summary>Outcome of a staging upsert, used to drive import diagnostics.</summary>
public readonly record struct UpsertResult(Guid Id, ImportAction Action);

public interface IExternalConnectionRepository
{
    Task<ExternalConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExternalConnection?> GetByProviderWorkspaceAsync(string providerType, string? workspaceId, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalConnection>> ListAsync(Guid agencyId, CancellationToken ct = default);
    Task<Guid> InsertAsync(ExternalConnection connection, CancellationToken ct = default);
    Task UpdateSyncAsync(Guid id, ExternalConnectionStatus status, DateTimeOffset attemptedAt, DateTimeOffset? successAt, CancellationToken ct = default);
}

public interface IExternalIdentityRepository
{
    Task<UpsertResult> UpsertAsync(ExternalIdentity identity, CancellationToken ct = default);
}

public interface IExternalContainerRepository
{
    Task<UpsertResult> UpsertAsync(ExternalContainer container, CancellationToken ct = default);
    Task<Guid?> GetIdByExternalAsync(Guid connectionId, ContainerType type, string externalId, CancellationToken ct = default);
}

public interface IExternalWorkItemRepository
{
    Task<UpsertResult> UpsertAsync(ExternalWorkItem item, CancellationToken ct = default);
    Task<Guid?> GetIdByExternalAsync(Guid connectionId, string externalId, CancellationToken ct = default);
}

public interface IExternalTimeEntryRepository
{
    Task<UpsertResult> UpsertAsync(ExternalTimeEntry entry, CancellationToken ct = default);
}

public interface IImportRunRepository
{
    Task<Guid> InsertAsync(ImportRun run, CancellationToken ct = default);
    Task UpdateAsync(ImportRun run, CancellationToken ct = default);
    Task<ImportRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ImportRun>> ListByConnectionAsync(Guid connectionId, int limit, CancellationToken ct = default);
}

public interface IImportRecordRepository
{
    Task InsertManyAsync(IEnumerable<ImportRecord> records, CancellationToken ct = default);
}

public interface ISyncCursorRepository
{
    Task<SyncCursor?> GetAsync(Guid connectionId, ExternalEntityType entityType, CancellationToken ct = default);
    Task UpsertAsync(SyncCursor cursor, CancellationToken ct = default);
}
