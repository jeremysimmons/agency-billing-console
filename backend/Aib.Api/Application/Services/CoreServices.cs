using Aib.Application;
using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Application.Integrations;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Aib.Application.Services;

public sealed class AgencyService(IAgencyRepository agencies, IClock clock)
{
    public async Task<AgencyDto> GetAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        return Map(agency);
    }

    public async Task<AgencyDto> UpdateUiPreferencesAsync(
        UpdateAgencyUiPreferencesRequest request,
        CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");

        agency.UiPreferences = SerializeUiPreferences(
            new AgencyUiPreferencesDto(request.TaskGroupClientOrder?.ToList() ?? []));
        agency.UpdatedAt = clock.UtcNow;
        await agencies.UpdateAsync(agency, ct);
        return Map(agency);
    }

    private static AgencyDto Map(Agency a) =>
        new(a.Id, a.Name, a.LastClickUpSyncAt, a.LastClickUpSyncSummary, ParseUiPreferences(a.UiPreferences));

    private static AgencyUiPreferencesDto ParseUiPreferences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}")
            return new AgencyUiPreferencesDto([]);

        try
        {
            var parsed = JsonSerializer.Deserialize<AgencyUiPreferencesPayload>(json, JsonOptions);
            return new AgencyUiPreferencesDto(parsed?.TaskGroupClientOrder ?? []);
        }
        catch (JsonException)
        {
            return new AgencyUiPreferencesDto([]);
        }
    }

    private static string SerializeUiPreferences(AgencyUiPreferencesDto prefs) =>
        JsonSerializer.Serialize(
            new AgencyUiPreferencesPayload { TaskGroupClientOrder = prefs.TaskGroupClientOrder.ToList() },
            JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed class AgencyUiPreferencesPayload
    {
        public List<Guid> TaskGroupClientOrder { get; set; } = [];
    }
}

public sealed class ClientService(
    IClientRepository clients,
    IAgencyRepository agencies,
    IClock clock)
{
    public async Task<IReadOnlyList<ClientDto>> ListAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var list = await clients.ListAsync(agency.Id, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ClientDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var client = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        return Map(client);
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Client name is required.");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var now = clock.UtcNow;
        var client = new Client
        {
            Id = Guid.NewGuid(),
            AgencyId = agency.Id,
            Name = request.Name.Trim(),
            Code = request.Code?.Trim(),
            OriginalName = request.OriginalName?.Trim(),
            Description = request.Description,
            Status = request.Status ?? ClientStatus.Active,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return Map(client);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Client name is required.");

        client.Name = request.Name.Trim();
        client.Code = request.Code?.Trim();
        client.OriginalName = request.OriginalName?.Trim();
        client.Description = request.Description;
        client.Status = request.Status;
        client.Active = request.Active;
        client.UpdatedAt = clock.UtcNow;
        await clients.UpdateAsync(client, ct);
        return Map(client);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _ = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        await clients.DeleteAsync(id, ct);
    }

    public async Task<DeleteAllClientsResult> DeleteAllAsync(CancellationToken ct = default)
    {
        var deleted = await clients.DeleteAllAsync(ct);
        return new DeleteAllClientsResult(deleted);
    }

    private static ClientDto Map(Client c) =>
        new(c.Id, c.Name, c.Code, c.OriginalName, c.ClickUpFolderId, c.ClickUpListId, c.Description, c.Status, c.Active,
            c.BillFieldAvailable);
}

public sealed class ProjectService(
    IProjectRepository projects,
    IClientRepository clients,
    IAgencyRepository agencies,
    IClock clock)
{
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await projects.ListAllAsync(ct);
        var names = await ClientNameMapAsync(ct);
        return list.Select(p => Map(p, names)).ToList();
    }

    public async Task<IReadOnlyList<ProjectDto>> ListByClientAsync(
        Guid clientId,
        bool includeShared = false,
        CancellationToken ct = default)
    {
        _ = await clients.GetByIdAsync(clientId, ct) ?? throw new NotFoundException("Client not found.");
        var list = await projects.ListByClientAsync(clientId, includeShared, ct);
        var names = await ClientNameMapAsync(ct);
        return list.Select(p => Map(p, names)).ToList();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var client = await clients.GetByIdAsync(request.ClientId, ct)
                     ?? throw new NotFoundException("Client not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Project name is required.");

        var now = clock.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        await projects.InsertAsync(project, ct);
        return Map(project, client.Name);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Project name is required.");
        var client = await clients.GetByIdAsync(request.ClientId, ct)
                     ?? throw new NotFoundException("Client not found.");

        project.Name = request.Name.Trim();
        project.ClientId = request.ClientId;
        project.UpdatedAt = clock.UtcNow;
        await projects.UpdateAsync(project, ct);
        return Map(project, client.Name);
    }

    private async Task<Dictionary<Guid, string>> ClientNameMapAsync(CancellationToken ct)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var list = await clients.ListAsync(agency.Id, ct);
        return list.ToDictionary(c => c.Id, c => c.Name);
    }

    private static ProjectDto Map(Project p, Dictionary<Guid, string> names) =>
        Map(p, names.GetValueOrDefault(p.ClientId, "Unknown"));

    private static ProjectDto Map(Project p, string clientName) =>
        new(p.Id, p.ClientId, clientName, p.Name);
}

public sealed class InvoiceService(
    IInvoiceRepository invoices,
    IClock clock,
    IOptions<InvoiceOptions> invoiceOptions)
{
    private decimal DefaultRate => invoiceOptions.Value.DefaultRate;

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await invoices.ListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Invoice name is required.");

        var name = request.Name.Trim();
        var existing = await invoices.GetByNameAsync(name, ct);
        if (existing is not null)
            throw new DomainException("An invoice with that name already exists.");

        var status = request.Status ?? InvoiceStatus.Preparing;
        if (!InvoiceStatus.All.Contains(status))
            throw new DomainException(
                "Invoice status must be preparing, sent, partially-paid, or fully-paid.");

        var isDefault = request.IsDefault;
        if (isDefault && status != InvoiceStatus.Preparing)
            throw new DomainException("Only a preparing invoice can be the default.");
        if (isDefault && InvoiceLabels.IsNone(name))
            throw new DomainException("The none invoice cannot be the default.");

        if (request.Rate is < 0)
            throw new DomainException("Invoice rate cannot be negative.");

        var includeNonBillable = request.IncludeNonBillableTasks ?? IncludeNonBillableTasks.None;
        if (!IncludeNonBillableTasks.All.Contains(includeNonBillable))
            throw new DomainException("Include non-billable tasks must be none, detail, or summary.");

        if (isDefault)
            await invoices.ClearDefaultsAsync(ct);

        var now = clock.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = status,
            SortOrder = await invoices.GetNextSortOrderAsync(ct),
            IsDefault = isDefault,
            Rate = request.Rate,
            IncludeNonBillableTasks = includeNonBillable,
            CreatedAt = now,
            UpdatedAt = now
        };
        await invoices.InsertAsync(invoice, ct);
        return Map(invoice);
    }

    public async Task<InvoiceDto> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await invoices.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Invoice name is required.");
        if (!InvoiceStatus.All.Contains(request.Status))
            throw new DomainException(
                "Invoice status must be preparing, sent, partially-paid, or fully-paid.");

        var name = request.Name.Trim();
        var conflict = await invoices.GetByNameAsync(name, ct);
        if (conflict is not null && conflict.Id != id)
            throw new DomainException("An invoice with that name already exists.");

        if (request.Rate is < 0)
            throw new DomainException("Invoice rate cannot be negative.");

        var includeNonBillable = request.IncludeNonBillableTasks ?? IncludeNonBillableTasks.None;
        if (!IncludeNonBillableTasks.All.Contains(includeNonBillable))
            throw new DomainException("Include non-billable tasks must be none, detail, or summary.");

        var isDefault = request.IsDefault;
        if (request.Status != InvoiceStatus.Preparing)
            isDefault = false;
        if (isDefault && InvoiceLabels.IsNone(name))
            throw new DomainException("The none invoice cannot be the default.");
        if (isDefault && request.Status != InvoiceStatus.Preparing)
            throw new DomainException("Only a preparing invoice can be the default.");

        if (isDefault && !invoice.IsDefault)
            await invoices.ClearDefaultsAsync(ct);

        invoice.Name = name;
        invoice.Status = request.Status;
        invoice.IsDefault = isDefault;
        invoice.Rate = request.Rate;
        invoice.IncludeNonBillableTasks = includeNonBillable;
        invoice.UpdatedAt = clock.UtcNow;
        await invoices.UpdateAsync(invoice, ct);
        return Map(invoice);
    }

    public async Task<IReadOnlyList<InvoiceDto>> ReorderAsync(ReorderInvoicesRequest request, CancellationToken ct = default)
    {
        var orderedIds = request.OrderedIds ?? [];
        var existing = await invoices.ListAsync(ct);
        if (orderedIds.Count != existing.Count
            || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => existing.All(e => e.Id != id)))
        {
            throw new DomainException("Invoice order must include each invoice exactly once.");
        }

        await invoices.ReorderAsync(orderedIds, clock.UtcNow, ct);
        return (await invoices.ListAsync(ct)).Select(Map).ToList();
    }

    private InvoiceDto Map(Invoice i) =>
        new(i.Id, i.Name, i.Status, i.SortOrder, i.IsDefault, i.Rate, i.Rate ?? DefaultRate, i.IncludeNonBillableTasks);
}

public sealed class InvoiceLineService(
    IInvoiceLineRepository lines,
    IInvoiceRepository invoices,
    IClientRepository clients,
    IProjectRepository projects,
    IClock clock)
{
    public async Task<IReadOnlyList<InvoiceLineDto>> ListAsync(Guid invoiceId, CancellationToken ct = default)
    {
        _ = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new NotFoundException("Invoice not found.");
        var list = await lines.ListByInvoiceAsync(invoiceId, ct);
        return await MapManyAsync(list, ct);
    }

    public async Task<InvoiceLineDto> CreateAsync(
        Guid invoiceId, CreateInvoiceLineRequest request, CancellationToken ct = default)
    {
        _ = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new NotFoundException("Invoice not found.");

        var (client, project, title, hours, flatFee, discount) = await ValidateAsync(request, ct);

        var now = clock.UtcNow;
        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            ClientId = client.Id,
            ProjectId = project?.Id,
            Title = title,
            Hours = hours,
            FlatFee = flatFee,
            DiscountPercent = discount,
            SortOrder = await lines.GetNextSortOrderAsync(invoiceId, ct),
            CreatedAt = now,
            UpdatedAt = now
        };
        await lines.InsertAsync(line, ct);
        return Map(line, client.Name, project?.Name);
    }

    public async Task<InvoiceLineDto> UpdateAsync(
        Guid invoiceId, Guid id, UpdateInvoiceLineRequest request, CancellationToken ct = default)
    {
        var line = await lines.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException("Invoice line not found.");
        if (line.InvoiceId != invoiceId)
            throw new NotFoundException("Invoice line not found.");

        var (client, project, title, hours, flatFee, discount) = await ValidateAsync(
            new CreateInvoiceLineRequest(
                request.ClientId, request.ProjectId, request.Title,
                request.Hours, request.FlatFee, request.DiscountPercent),
            ct);

        line.ClientId = client.Id;
        line.ProjectId = project?.Id;
        line.Title = title;
        line.Hours = hours;
        line.FlatFee = flatFee;
        line.DiscountPercent = discount;
        line.UpdatedAt = clock.UtcNow;
        await lines.UpdateAsync(line, ct);
        return Map(line, client.Name, project?.Name);
    }

    public async Task DeleteAsync(Guid invoiceId, Guid id, CancellationToken ct = default)
    {
        var line = await lines.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException("Invoice line not found.");
        if (line.InvoiceId != invoiceId)
            throw new NotFoundException("Invoice line not found.");
        await lines.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<InvoiceLineDto>> ReorderAsync(
        Guid invoiceId, ReorderInvoiceLinesRequest request, CancellationToken ct = default)
    {
        _ = await invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new NotFoundException("Invoice not found.");

        var orderedIds = request.OrderedIds ?? [];
        var existing = await lines.ListByInvoiceAsync(invoiceId, ct);
        if (orderedIds.Count != existing.Count
            || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => existing.All(e => e.Id != id)))
        {
            throw new DomainException("Invoice line order must include each line exactly once.");
        }

        await lines.ReorderAsync(invoiceId, orderedIds, clock.UtcNow, ct);
        return await MapManyAsync(await lines.ListByInvoiceAsync(invoiceId, ct), ct);
    }

    private async Task<(Client Client, Project? Project, string Title, decimal Hours, decimal? FlatFee, decimal Discount)>
        ValidateAsync(CreateInvoiceLineRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("Line title is required.");

        var client = await clients.GetByIdAsync(request.ClientId, ct)
                     ?? throw new DomainException("Client not found.");

        Project? project = null;
        if (request.ProjectId is { } projectId)
        {
            project = await projects.GetByIdAsync(projectId, ct)
                      ?? throw new DomainException("Project not found.");
            if (project.ClientId != client.Id)
            {
                var projectClient = await clients.GetByIdAsync(project.ClientId, ct);
                if (!SharedClients.IsShared(projectClient?.Name))
                    throw new DomainException("Project must belong to the selected client, or Shared.");
            }
        }

        if (request.Hours < 0)
            throw new DomainException("Hours cannot be negative.");
        if (request.FlatFee is < 0)
            throw new DomainException("Flat fee cannot be negative.");
        if (request.DiscountPercent is < 0 or > 100)
            throw new DomainException("Discount must be between 0 and 100.");

        if (request.FlatFee is null && request.Hours <= 0)
            throw new DomainException("Enter hours or a flat fee.");

        var hours = request.FlatFee is not null ? 0m : request.Hours;
        var flatFee = request.FlatFee;
        return (client, project, request.Title.Trim(), hours, flatFee, request.DiscountPercent);
    }

    private async Task<IReadOnlyList<InvoiceLineDto>> MapManyAsync(
        IReadOnlyList<InvoiceLine> list, CancellationToken ct)
    {
        var clientNames = new Dictionary<Guid, string>();
        var projectNames = new Dictionary<Guid, string?>();
        var result = new List<InvoiceLineDto>(list.Count);
        foreach (var line in list)
        {
            if (!clientNames.TryGetValue(line.ClientId, out var clientName))
            {
                var client = await clients.GetByIdAsync(line.ClientId, ct);
                clientName = client?.Name ?? "Unknown";
                clientNames[line.ClientId] = clientName;
            }

            string? projectName = null;
            if (line.ProjectId is { } pid)
            {
                if (!projectNames.TryGetValue(pid, out projectName))
                {
                    var project = await projects.GetByIdAsync(pid, ct);
                    projectName = project?.Name;
                    projectNames[pid] = projectName;
                }
            }

            result.Add(Map(line, clientName, projectName));
        }
        return result;
    }

    private static InvoiceLineDto Map(InvoiceLine line, string clientName, string? projectName) =>
        new(
            line.Id, line.InvoiceId, line.ClientId, clientName, line.ProjectId, projectName,
            line.Title, line.Hours, line.FlatFee, line.DiscountPercent, line.SortOrder);
}

public sealed class TaskService(
    ITaskRepository tasks,
    IClientRepository clients,
    IProjectRepository projects,
    IInvoiceRepository invoices,
    IClickUpClient clickUp,
    IOptions<ClickUpOptions> clickUpOptions,
    IClock clock,
    ILogger<TaskService> logger)
{
    private readonly ClickUpOptions _clickUp = clickUpOptions.Value;
    public async Task<IReadOnlyList<TaskDto>> ListAsync(
        Guid? clientId,
        bool? missingOnly,
        IReadOnlyList<string>? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId = null,
        string? clickUpFolderId = null,
        string? clickUpSpaceId = null,
        string? invoiceLabel = null,
        CancellationToken ct = default)
    {
        var matched = await tasks.ListAsync(
            clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            clickUpListId, clickUpFolderId, clickUpSpaceId, invoiceLabel, ct);
        var list = OrderWithChildrenAfterParents(await IncludeAncestorTasksAsync(matched, ct));
        var clientNames = new Dictionary<Guid, string>();
        var projectNames = new Dictionary<Guid, string>();

        var result = new List<TaskDto>();
        foreach (var task in list)
        {
            if (!clientNames.TryGetValue(task.ClientId, out var clientName))
            {
                var client = await clients.GetByIdAsync(task.ClientId, ct);
                clientName = client?.Name ?? "Unknown";
                clientNames[task.ClientId] = clientName;
            }

            string? projectName = null;
            if (task.ProjectId is { } pid)
            {
                if (!projectNames.TryGetValue(pid, out projectName))
                {
                    var project = await projects.GetByIdAsync(pid, ct);
                    projectName = project?.Name;
                    projectNames[pid] = projectName ?? string.Empty;
                }
            }

            result.Add(Map(task, clientName, projectName));
        }
        return result;
    }

    /// <summary>
    /// Pull in ClickUp parents/ancestors missing from a filtered result so children
    /// are not shown as orphans when the parent failed the filter (e.g. missing-only).
    /// </summary>
    private async Task<IReadOnlyList<WorkTask>> IncludeAncestorTasksAsync(
        IReadOnlyList<WorkTask> matched,
        CancellationToken ct)
    {
        if (matched.Count == 0) return matched;

        var byClickUpId = new Dictionary<string, WorkTask>(StringComparer.Ordinal);
        foreach (var task in matched)
        {
            if (!string.IsNullOrEmpty(task.ClickUpTaskId))
                byClickUpId.TryAdd(task.ClickUpTaskId, task);
        }

        var extras = new List<WorkTask>();
        var pending = new Queue<string>();
        foreach (var task in matched)
        {
            if (!string.IsNullOrEmpty(task.ClickUpParentId) && !byClickUpId.ContainsKey(task.ClickUpParentId))
                pending.Enqueue(task.ClickUpParentId);
        }

        while (pending.Count > 0)
        {
            var parentId = pending.Dequeue();
            if (byClickUpId.ContainsKey(parentId)) continue;

            var parent = await tasks.GetByClickUpTaskIdAsync(parentId, ct);
            if (parent is null) continue;

            byClickUpId[parentId] = parent;
            extras.Add(parent);
            if (!string.IsNullOrEmpty(parent.ClickUpParentId) && !byClickUpId.ContainsKey(parent.ClickUpParentId))
                pending.Enqueue(parent.ClickUpParentId);
        }

        if (extras.Count == 0) return matched;

        var combined = new List<WorkTask>(matched.Count + extras.Count);
        combined.AddRange(matched);
        combined.AddRange(extras);
        return combined;
    }

    public async Task<TaskSummaryDto> GetSummaryAsync(
        Guid? clientId,
        bool? missingOnly,
        IReadOnlyList<string>? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId = null,
        string? clickUpFolderId = null,
        string? clickUpSpaceId = null,
        string? invoiceLabel = null,
        CancellationToken ct = default)
    {
        var (byClient, byDoneMonth) = await tasks.GetSummaryAsync(
            clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            clickUpListId, clickUpFolderId, clickUpSpaceId, invoiceLabel, ct);
        return new TaskSummaryDto(
            byClient.Select(r => new TaskClientCountDto(r.ClientId, r.ClientName, r.TaskCount, r.MissingCount, r.UninvoicedCount)).ToList(),
            byDoneMonth.Select(r => new TaskMonthCountDto(r.Month, r.TaskCount, r.MissingCount, r.UninvoicedCount)).ToList());
    }

    public async Task<TaskFilterOptionsDto> GetFilterOptionsAsync(Guid? clientId, CancellationToken ct = default)
    {
        var (createdMonths, doneMonths, statuses) = await tasks.ListFilterOptionsAsync(clientId, ct);
        return new TaskFilterOptionsDto(createdMonths, doneMonths, statuses);
    }

    public async Task<TaskDto> UpdatePrepAsync(Guid id, UpdateTaskPrepRequest request, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        if (request.ProjectId is { } projectId)
        {
            var project = await projects.GetByIdAsync(projectId, ct)
                          ?? throw new NotFoundException("Project not found.");
            if (project.ClientId != task.ClientId)
            {
                var projectClient = await clients.GetByIdAsync(project.ClientId, ct);
                if (!SharedClients.IsShared(projectClient?.Name))
                    throw new DomainException("Project must belong to the same client as the task, or Shared.");
            }
        }

        task.ProjectId = request.ProjectId;
        task.Bill = request.Bill;
        task.BillableHours = request.BillableHours;
        task.NonBillableHours = request.NonBillableHours;
        task.InvoiceLabel = request.InvoiceLabel;
        task.FlatFee = NormalizeMoney(request.FlatFee);
        task.Note = request.Note;
        ApplyInvoiceForBill(task);
        ApplyHoursForBill(task);
        if (request.ProjectId is not null)
            await ApplyDefaultInvoiceForBillableAsync(task, ct);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);
        if (request.ProjectId is { } assignedProjectId)
            await PropagateProjectToUnassignedChildrenAsync(task, assignedProjectId, ct);
        await SyncBillToClickUpAsync(task, ct);
        await EnsureSelfAssignedOnClickUpAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;

        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    public async Task<TaskDto> UpdateBillAsync(Guid id, string? bill, CancellationToken ct = default)
    {
        var normalized = NormalizeBill(bill);
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");

        task.Bill = normalized;
        ApplyInvoiceForBill(task);
        ApplyHoursForBill(task);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);
        await SyncBillToClickUpAsync(task, ct);
        await EnsureSelfAssignedOnClickUpAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;

        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    public async Task<TaskDto> UpdateProjectAsync(Guid id, Guid? projectId, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        string? projectName = null;
        if (projectId is { } pid)
        {
            var project = await projects.GetByIdAsync(pid, ct)
                          ?? throw new NotFoundException("Project not found.");
            if (project.ClientId != task.ClientId)
            {
                var projectClient = await clients.GetByIdAsync(project.ClientId, ct);
                if (!SharedClients.IsShared(projectClient?.Name))
                    throw new DomainException("Project must belong to the same client as the task, or Shared.");
            }
            projectName = project.Name;
        }

        task.ProjectId = projectId;
        if (projectId is not null)
            await ApplyDefaultInvoiceForBillableAsync(task, ct);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);
        if (projectId is { } assignedProjectId)
            await PropagateProjectToUnassignedChildrenAsync(task, assignedProjectId, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    private async Task PropagateProjectToUnassignedChildrenAsync(
        WorkTask parent,
        Guid projectId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parent.ClickUpTaskId))
            return;
        string? defaultInvoiceLabel = null;
        var defaultInvoice = await invoices.GetDefaultAsync(ct);
        if (defaultInvoice is not null
            && defaultInvoice.Status == InvoiceStatus.Preparing
            && !InvoiceLabels.IsNone(defaultInvoice.Name))
            defaultInvoiceLabel = defaultInvoice.Name;

        await tasks.AssignProjectToUnassignedDescendantsAsync(
            parent.ClickUpTaskId,
            projectId,
            clock.UtcNow,
            defaultInvoiceLabel,
            ct);
    }

    private async Task ApplyDefaultInvoiceForBillableAsync(WorkTask task, CancellationToken ct)
    {
        if (!string.Equals(task.Bill?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            return;
        var defaultInvoice = await invoices.GetDefaultAsync(ct);
        if (defaultInvoice is null
            || defaultInvoice.Status != InvoiceStatus.Preparing
            || InvoiceLabels.IsNone(defaultInvoice.Name))
            return;
        task.InvoiceLabel = defaultInvoice.Name;
    }

    private static void ApplyInvoiceForBill(WorkTask task)
    {
        if (!string.Equals(task.Bill?.Trim(), "no", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(task.InvoiceLabel))
            task.InvoiceLabel = InvoiceLabels.None;
    }

    /// <summary>
    /// Fill empty hours from ClickUp tracked hours when bill is set.
    /// Bill=no with no ClickUp hours → non-billable 0.
    /// </summary>
    private static void ApplyHoursForBill(WorkTask task)
    {
        var billNorm = task.Bill?.Trim();
        if (string.Equals(billNorm, "yes", StringComparison.OrdinalIgnoreCase)
            && task.BillableHours is null
            && task.ActualHours is not null)
        {
            task.BillableHours = task.ActualHours;
            return;
        }

        if (string.Equals(billNorm, "no", StringComparison.OrdinalIgnoreCase)
            && task.NonBillableHours is null)
        {
            task.NonBillableHours = task.ActualHours ?? 0;
        }
    }

    public async Task<TaskDto> UpdateInvoiceAsync(Guid id, string? invoiceLabel, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        task.InvoiceLabel = string.IsNullOrWhiteSpace(invoiceLabel) ? null : invoiceLabel.Trim();
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;
        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    public async Task<TaskDto> UpdateDiscountAsync(Guid id, decimal discountPercent, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        task.DiscountPercent = NormalizeDiscountPercent(discountPercent);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;
        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    public async Task<TaskDto> UpdateFlatFeeAsync(Guid id, decimal? flatFee, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        task.FlatFee = NormalizeMoney(flatFee);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;
        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    public async Task<TaskHoursUpdateDto> UpdateBillableHoursAsync(Guid id, decimal? billableHours, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        task.BillableHours = NormalizeHours(billableHours);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);

        var (trackedHours, warning) = await SyncBillableHoursToClickUpAsync(task, ct);
        if (trackedHours is not null && task.ActualHours != trackedHours)
        {
            task.ActualHours = trackedHours;
            task.UpdatedAt = clock.UtcNow;
            await tasks.UpdateAsync(task, ct);
        }

        return await MapHoursUpdate(task, trackedHours ?? task.ActualHours, warning, ct);
    }

    public async Task<TaskHoursUpdateDto> UpdateNonBillableHoursAsync(Guid id, decimal? nonBillableHours, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        task.NonBillableHours = NormalizeHours(nonBillableHours);
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);
        return await MapHoursUpdate(task, task.ActualHours, null, ct);
    }

    private async Task<TaskHoursUpdateDto> MapHoursUpdate(
        WorkTask task,
        decimal? clickUpTrackedHours,
        string? warning,
        CancellationToken ct)
    {
        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;
        return new TaskHoursUpdateDto(Map(task, client?.Name ?? "Unknown", projectName), clickUpTrackedHours, warning);
    }

    private async Task<(decimal? TrackedHours, string? Warning)> SyncBillableHoursToClickUpAsync(
        WorkTask task,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(task.ClickUpTaskId)
            || !_clickUp.IsConfigured
            || string.IsNullOrWhiteSpace(_clickUp.TeamId))
        {
            return (null, null);
        }

        if (!long.TryParse(_clickUp.AssigneeId, out var assigneeId))
            return (null, "ClickUp assignee is not configured.");

        var trackedHours = await clickUp.GetTaskTimeSpentHoursAsync(task.ClickUpTaskId, ct);
        var targetHours = task.BillableHours ?? 0;
        var diff = targetHours - trackedHours;

        if (Math.Abs(diff) <= 0.01m)
            return (trackedHours, null);

        if (diff > 0.01m)
        {
            try
            {
                var durationMs = (long)Math.Round(diff * 3_600_000m, MidpointRounding.AwayFromZero);
                var startMs = clock.UtcNow.ToUnixTimeMilliseconds() - durationMs;
                await clickUp.CreateTimeEntryAsync(
                    _clickUp.TeamId,
                    task.ClickUpTaskId,
                    startMs,
                    durationMs,
                    assigneeId,
                    billable: true,
                    "Billing prep adjustment",
                    ct);
                var updatedTracked = await clickUp.GetTaskTimeSpentHoursAsync(task.ClickUpTaskId, ct);
                if (Math.Abs(targetHours - updatedTracked) > 0.01m)
                {
                    return (updatedTracked,
                        $"Added {diff:0.##}h to ClickUp; tracked is now {updatedTracked:0.##}h (billable {targetHours:0.##}h).");
                }

                return (updatedTracked, null);
            }
            catch (Exception ex)
            {
                return (trackedHours, $"Billable hours saved locally. Could not add ClickUp time entry: {ex.Message}");
            }
        }

        return (trackedHours, $"ClickUp tracked {trackedHours:0.##}h but billable is {targetHours:0.##}h.");
    }

    private static decimal? NormalizeHours(decimal? hours)
    {
        if (hours is null)
            return null;
        if (hours < 0)
            throw new DomainException("Hours cannot be negative.");
        return Math.Round(hours.Value, 2);
    }

    private static decimal? NormalizeMoney(decimal? amount)
    {
        if (amount is null)
            return null;
        if (amount < 0)
            throw new DomainException("Flat fee cannot be negative.");
        return Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
    }

    private async Task SyncBillToClickUpAsync(WorkTask task, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(task.ClickUpTaskId))
            return;
        if (!_clickUp.IsConfigured)
            return;

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        if (client is null || !client.BillFieldAvailable)
            return;

        var fieldId = client.BillCustomFieldId ?? _clickUp.BillCustomFieldId;
        if (string.IsNullOrWhiteSpace(fieldId))
            return;

        try
        {
            var value = BillToClickUpValue(task.Bill, client);
            await clickUp.SetTaskCustomFieldAsync(task.ClickUpTaskId, fieldId, value, ct);
        }
        catch (Exception ex)
        {
            if (IsMissingBillFieldError(ex))
            {
                client.BillFieldAvailable = false;
                client.BillFieldCheckedAt = clock.UtcNow;
                client.UpdatedAt = clock.UtcNow;
                await clients.UpdateAsync(client, ct);
                logger.LogWarning(
                    ex,
                    "Billable field unavailable for client {ClientId}; marked bill_field_available=false",
                    client.Id);
                return;
            }

            logger.LogWarning(
                ex,
                "Failed to sync bill={Bill} to ClickUp for task {TaskId} ({ClickUpTaskId})",
                task.Bill,
                task.Id,
                task.ClickUpTaskId);
        }
    }

    /// <summary>
    /// When bill=yes, ensure the configured ClickUp user is an assignee so the task
    /// stays in the assignee-filtered sync set.
    /// </summary>
    private async Task EnsureSelfAssignedOnClickUpAsync(WorkTask task, CancellationToken ct)
    {
        if (!string.Equals(task.Bill?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(task.ClickUpTaskId))
            return;
        if (!_clickUp.IsConfigured || string.IsNullOrWhiteSpace(_clickUp.AssigneeId))
            return;
        if (!long.TryParse(_clickUp.AssigneeId, out var assigneeId))
        {
            logger.LogWarning("ClickUp assignee id {AssigneeId} is not numeric; cannot add assignee", _clickUp.AssigneeId);
            return;
        }

        try
        {
            var remote = await clickUp.GetTaskAsync(task.ClickUpTaskId, ct);
            var alreadyAssigned = remote.AssigneeIds.Any(id =>
                string.Equals(id, _clickUp.AssigneeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, assigneeId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (alreadyAssigned)
                return;

            await clickUp.AddTaskAssigneesAsync(task.ClickUpTaskId, [assigneeId], ct);
            logger.LogInformation(
                "Added ClickUp assignee {AssigneeId} to task {ClickUpTaskId} after bill=yes",
                assigneeId,
                task.ClickUpTaskId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to add ClickUp assignee {AssigneeId} to task {TaskId} ({ClickUpTaskId})",
                _clickUp.AssigneeId,
                task.Id,
                task.ClickUpTaskId);
        }
    }

    private string? BillToClickUpValue(string? bill, Client client)
    {
        if (string.IsNullOrWhiteSpace(bill))
            return null;
        if (string.Equals(bill, "yes", StringComparison.OrdinalIgnoreCase))
            return client.BillYesOptionId ?? _clickUp.BillYesOptionId;
        if (string.Equals(bill, "no", StringComparison.OrdinalIgnoreCase))
            return client.BillNoOptionId ?? _clickUp.BillNoOptionId;
        throw new DomainException("Bill must be yes, no, or empty.");
    }

    private static bool IsMissingBillFieldError(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("FIELD_115", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Custom field does not exist", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Field not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeBill(string? bill)
    {
        if (string.IsNullOrWhiteSpace(bill))
            return null;
        return bill.Trim().ToLowerInvariant() switch
        {
            "yes" => "yes",
            "no" => "no",
            _ => throw new DomainException("Bill must be yes, no, or empty."),
        };
    }

    private static decimal NormalizeDiscountPercent(decimal discountPercent)
    {
        if (discountPercent is < 0 or > 100)
            throw new DomainException("Discount percent must be between 0 and 100.");
        return Math.Round(discountPercent, 2, MidpointRounding.AwayFromZero);
    }

    private static TaskDto Map(WorkTask t, string clientName, string? projectName) =>
        new(
            t.Id, t.ShortId, t.ClientId, clientName, t.ProjectId, projectName,
            t.Bill, t.BillableHours, t.NonBillableHours, t.InvoiceLabel, t.DiscountPercent, t.FlatFee, t.Note,
            t.ClickUpUrl, t.ClickUpTaskId, t.ClickUpParentId,
            t.ClickUpFolderId, t.ClickUpFolderName, t.ClickUpListId, t.ClickUpListName,
            t.Title, t.Description, t.ClickUpStatus, t.Tags,
            t.DateCreated, t.DueDate, t.DateDone, t.DateClosed,
            t.OrderIndex, t.EstimatedHours, t.ActualHours,
            NeedsAttention(t));

    /// <summary>
    /// Keep existing list order for roots; emit each child immediately after its parent (DFS).
    /// Orphans (parent not in the result set) stay as roots.
    /// </summary>
    internal static IReadOnlyList<WorkTask> OrderWithChildrenAfterParents(IReadOnlyList<WorkTask> tasks)
    {
        if (tasks.Count <= 1) return tasks;

        var byClickUpId = new Dictionary<string, WorkTask>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            if (!string.IsNullOrEmpty(task.ClickUpTaskId))
                byClickUpId.TryAdd(task.ClickUpTaskId, task);
        }

        var childrenByParent = new Dictionary<string, List<WorkTask>>(StringComparer.Ordinal);
        var roots = new List<WorkTask>();
        foreach (var task in tasks)
        {
            var parentId = task.ClickUpParentId;
            if (!string.IsNullOrEmpty(parentId) && byClickUpId.ContainsKey(parentId))
            {
                if (!childrenByParent.TryGetValue(parentId, out var kids))
                {
                    kids = [];
                    childrenByParent[parentId] = kids;
                }
                kids.Add(task);
            }
            else
            {
                roots.Add(task);
            }
        }

        foreach (var kids in childrenByParent.Values)
        {
            kids.Sort(static (a, b) =>
            {
                var byIndex = Nullable.Compare(a.OrderIndex, b.OrderIndex);
                return byIndex != 0 ? byIndex : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            });
        }

        var ordered = new List<WorkTask>(tasks.Count);
        var seen = new HashSet<Guid>();

        void Visit(WorkTask task)
        {
            if (!seen.Add(task.Id)) return;
            ordered.Add(task);
            if (task.ClickUpTaskId is { } id && childrenByParent.TryGetValue(id, out var kids))
            {
                foreach (var child in kids)
                    Visit(child);
            }
        }

        foreach (var root in roots)
            Visit(root);

        return ordered;
    }

    private static bool NeedsAttention(WorkTask t) =>
        !IsComplete(t)
        && (string.IsNullOrWhiteSpace(t.Bill)
            || HasMissingHours(t)
            || string.IsNullOrWhiteSpace(t.InvoiceLabel));

    private static bool IsComplete(WorkTask t) =>
        string.Equals(t.ClickUpStatus?.Trim(), "cancelled", StringComparison.OrdinalIgnoreCase)
        && string.Equals(t.Bill?.Trim(), "no", StringComparison.OrdinalIgnoreCase);

    private static bool HasMissingHours(WorkTask t) =>
        string.Equals(t.Bill, "yes", StringComparison.OrdinalIgnoreCase)
        && t.FlatFee is null
        && !((t.BillableHours is not null || t.NonBillableHours is not null)
             && ((t.BillableHours ?? 0) > 0 || (t.NonBillableHours ?? 0) > 0));
}
