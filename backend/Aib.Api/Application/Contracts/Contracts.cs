using Aib.Domain;

namespace Aib.Application.Contracts;

public sealed record AgencyDto(
    Guid Id, string Name, DateTimeOffset? LastClickUpSyncAt, string? LastClickUpSyncSummary,
    AgencyUiPreferencesDto UiPreferences);

public sealed record AgencyUiPreferencesDto(IReadOnlyList<Guid> TaskGroupClientOrder);

public sealed record UpdateAgencyUiPreferencesRequest(IReadOnlyList<Guid> TaskGroupClientOrder);

public sealed record CreateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus? Status);
public sealed record UpdateClientRequest(string Name, string? Code, string? OriginalName, string? Description, ClientStatus Status, bool Active);
public sealed record ClientDto(
    Guid Id, string Name, string? Code, string? OriginalName, string? ClickUpFolderId, string? ClickUpListId,
    string? Description, ClientStatus Status, bool Active,
    bool BillFieldAvailable);

public sealed record ClickUpSyncProgressEvent(
    string Phase,
    string? Message = null,
    int? ContainersUpserted = null,
    int? Page = null,
    int? TasksCreated = null,
    int? TasksUpdated = null,
    int? ClientsCreated = null,
    int? ClientsProcessed = null,
    int? ClientsTotal = null,
    DateTimeOffset? SyncedAt = null,
    string? Summary = null,
    string? Error = null);
public sealed record DeleteAllClientsResult(int Deleted);

public sealed record CreateProjectRequest(Guid ClientId, string Name);
public sealed record UpdateProjectRequest(string Name);
public sealed record ProjectDto(Guid Id, Guid ClientId, string Name);

public sealed record CreateInvoiceRequest(string Name, InvoiceStatus? Status = null);
public sealed record UpdateInvoiceRequest(string Name, InvoiceStatus Status);
public sealed record ReorderInvoicesRequest(IReadOnlyList<Guid> OrderedIds);
public sealed record InvoiceDto(Guid Id, string Name, InvoiceStatus Status, int SortOrder);

public sealed record UpdateTaskPrepRequest(
    Guid? ProjectId, string? Bill, decimal? BillableHours, decimal? NonBillableHours,
    string? InvoiceLabel, string? Note);

public sealed record UpdateTaskBillRequest(string? Bill);

public sealed record UpdateTaskProjectRequest(Guid? ProjectId);

public sealed record UpdateTaskInvoiceRequest(string? InvoiceLabel);

public sealed record UpdateTaskHoursRequest(decimal? Hours);

public sealed record TaskHoursUpdateDto(
    TaskDto Task,
    decimal? ClickUpTrackedHours,
    string? Warning);

public sealed record TaskDto(
    Guid Id, int ShortId, Guid ClientId, string ClientName, Guid? ProjectId, string? ProjectName,
    string? Bill, decimal? BillableHours, decimal? NonBillableHours, string? InvoiceLabel, string? Note,
    string? ClickUpUrl, string? ClickUpTaskId, string? ClickUpParentId,
    string? ClickUpFolderId, string? ClickUpFolderName, string? ClickUpListId, string? ClickUpListName,
    string Title, string? Description, string? ClickUpStatus, string? Tags,
    DateTimeOffset? DateCreated, DateTimeOffset? DueDate, DateTimeOffset? DateDone, DateTimeOffset? DateClosed,
    long? OrderIndex, decimal? EstimatedHours, decimal? ActualHours,
    bool NeedsAttention);

public sealed record ClickUpHierarchyNodeDto(
    string Type,
    string Id,
    string Name,
    string? ParentType,
    string? ParentId,
    DateTimeOffset UpdatedAt,
    int TaskCount,
    IReadOnlyList<ClickUpHierarchyNodeDto> Children);

public sealed record ClickUpSyncResultDto(
    DateTimeOffset SyncedAt,
    int ContainersUpserted,
    int TasksCreated,
    int TasksUpdated,
    int ClientsCreated,
    string Summary);

public sealed record CsvImportResultDto(int Imported, int Updated, int Skipped, string Summary);

public sealed record TaskFilterOptionsDto(
    IReadOnlyList<string> CreatedMonths,
    IReadOnlyList<string> DoneMonths,
    IReadOnlyList<string> Statuses);

public sealed record TaskClientCountDto(
    Guid ClientId, string ClientName, int TaskCount, int MissingCount, int UninvoicedCount);

public sealed record TaskMonthCountDto(
    string Month, int TaskCount, int MissingCount, int UninvoicedCount);

public sealed record TaskSummaryDto(
    IReadOnlyList<TaskClientCountDto> ByClient,
    IReadOnlyList<TaskMonthCountDto> ByDoneMonth);
