using Aib.Domain;

namespace Aib.Application.Contracts;

public sealed record AgencyDto(
    Guid Id, string Name, DateTimeOffset? LastClickUpSyncAt, string? LastClickUpSyncSummary);

public sealed record CreateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus? Status);
public sealed record UpdateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus Status, bool Active);
public sealed record ClientDto(
    Guid Id, string Name, string? Code, string? OriginalName, string? ClickUpFolderId,
    string? Description, ClientStatus Status, bool Active);
public sealed record DeleteAllClientsResult(int Deleted);

public sealed record CreateProjectRequest(Guid ClientId, string Name);
public sealed record UpdateProjectRequest(string Name);
public sealed record ProjectDto(Guid Id, Guid ClientId, string Name);

public sealed record UpdateTaskPrepRequest(
    Guid? ProjectId, string? Bill, decimal? BillableHours, decimal? NonBillableHours,
    string? InvoiceLabel, string? Note);

public sealed record TaskDto(
    Guid Id, Guid ClientId, string ClientName, Guid? ProjectId, string? ProjectName,
    string? Bill, decimal? BillableHours, decimal? NonBillableHours, string? InvoiceLabel, string? Note,
    string? ClickUpUrl, string? ClickUpTaskId, string? ClickUpParentId,
    string? ClickUpFolderId, string? ClickUpFolderName, string? ClickUpListId, string? ClickUpListName,
    string Title, string? Description, string? ClickUpStatus, string? Tags,
    DateTimeOffset? DateCreated, DateTimeOffset? DueDate, DateTimeOffset? DateDone, DateTimeOffset? DateClosed,
    long? OrderIndex, decimal? EstimatedHours, decimal? ActualHours,
    bool NeedsAttention);

public sealed record ClickUpHierarchyNodeDto(
    string Type, string Id, string Name, IReadOnlyList<ClickUpHierarchyNodeDto> Children);

public sealed record ClickUpSyncResultDto(
    DateTimeOffset SyncedAt,
    int ContainersUpserted,
    int TasksCreated,
    int TasksUpdated,
    int ClientsCreated,
    string Summary);

public sealed record CsvImportResultDto(int Imported, int Updated, int Skipped, string Summary);
