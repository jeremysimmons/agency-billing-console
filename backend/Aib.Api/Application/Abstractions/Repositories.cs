using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Abstractions;

public sealed record TaskClientCountRow(
    Guid ClientId, string ClientName, int TaskCount, int MissingCount, int UninvoicedCount);

public sealed record TaskMonthCountRow(
    string Month, int TaskCount, int MissingCount, int UninvoicedCount);

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByNameAsync(Guid agencyId, string name, CancellationToken ct = default);
    Task<Client?> GetByClickUpFolderIdAsync(string folderId, CancellationToken ct = default);
    Task<Client?> GetByClickUpListIdAsync(string listId, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListAsync(Guid agencyId, CancellationToken ct = default);
    Task<Guid> InsertAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteAllAsync(CancellationToken ct = default);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByClientAsync(Guid clientId, bool includeShared = false, CancellationToken ct = default);
    Task<Guid> InsertAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct = default);
    Task<int> GetNextSortOrderAsync(CancellationToken ct = default);
    Task<Guid> InsertAsync(Invoice invoice, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
    Task ReorderAsync(IReadOnlyList<Guid> orderedIds, DateTimeOffset updatedAt, CancellationToken ct = default);
}

public interface ITaskRepository
{
    Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkTask?> GetByClickUpUrlAsync(string url, CancellationToken ct = default);
    Task<WorkTask?> GetByClickUpTaskIdAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid? clientId,
        bool? missingOnly,
        string? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId,
        string? clickUpFolderId,
        string? clickUpSpaceId,
        CancellationToken ct = default);
    Task<(IReadOnlyList<TaskClientCountRow> ByClient, IReadOnlyList<TaskMonthCountRow> ByDoneMonth)> GetSummaryAsync(
        Guid? clientId,
        bool? missingOnly,
        string? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId,
        string? clickUpFolderId,
        string? clickUpSpaceId,
        CancellationToken ct = default);
    Task<(IReadOnlyList<string> CreatedMonths, IReadOnlyList<string> DoneMonths, IReadOnlyList<string> Statuses)> ListFilterOptionsAsync(
        Guid? clientId, CancellationToken ct = default);
    Task<Guid> InsertAsync(WorkTask task, CancellationToken ct = default);
    Task UpdateAsync(WorkTask task, CancellationToken ct = default);
    Task UpdateApiFieldsAsync(WorkTask task, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, int>> CountByClickUpListIdAsync(CancellationToken ct = default);
}

public interface IClickUpContainerRepository
{
    Task UpsertManyAsync(IReadOnlyList<ClickUpContainer> containers, CancellationToken ct = default);
    Task<IReadOnlyList<ClickUpContainer>> ListAllAsync(CancellationToken ct = default);
}

public interface IAgencyRepository
{
    Task<Agency?> GetDefaultAsync(CancellationToken ct = default);
    Task<Guid> InsertAsync(Agency agency, CancellationToken ct = default);
    Task UpdateAsync(Agency agency, CancellationToken ct = default);
    Task UpdateSyncSummaryAsync(Guid id, DateTimeOffset syncedAt, string summary, CancellationToken ct = default);
}
