using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Abstractions;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByClickUpFolderIdAsync(string folderId, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListAsync(Guid agencyId, CancellationToken ct = default);
    Task<Guid> InsertAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteAllAsync(CancellationToken ct = default);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<Guid> InsertAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

public interface ITaskRepository
{
    Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkTask?> GetByClickUpUrlAsync(string url, CancellationToken ct = default);
    Task<WorkTask?> GetByClickUpTaskIdAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid? clientId,
        bool? missingOnly,
        bool? includeInvoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        CancellationToken ct = default);
    Task<(IReadOnlyList<string> CreatedMonths, IReadOnlyList<string> DoneMonths, IReadOnlyList<string> Statuses)> ListFilterOptionsAsync(
        Guid? clientId, CancellationToken ct = default);
    Task<Guid> InsertAsync(WorkTask task, CancellationToken ct = default);
    Task UpdateAsync(WorkTask task, CancellationToken ct = default);
    Task UpdateApiFieldsAsync(WorkTask task, CancellationToken ct = default);
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
