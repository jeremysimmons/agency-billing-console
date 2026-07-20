using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class AgencyService(IAgencyRepository agencies)
{
    public async Task<AgencyDto> GetAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        return Map(agency);
    }

    private static AgencyDto Map(Agency a) =>
        new(a.Id, a.Name, a.LastClickUpSyncAt, a.LastClickUpSyncSummary);
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
        new(c.Id, c.Name, c.Code, c.OriginalName, c.ClickUpFolderId, c.Description, c.Status, c.Active);
}

public sealed class ProjectService(
    IProjectRepository projects,
    IClientRepository clients,
    IClock clock)
{
    public async Task<IReadOnlyList<ProjectDto>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        _ = await clients.GetByIdAsync(clientId, ct) ?? throw new NotFoundException("Client not found.");
        var list = await projects.ListByClientAsync(clientId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        _ = await clients.GetByIdAsync(request.ClientId, ct) ?? throw new NotFoundException("Client not found.");
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
        return Map(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Project name is required.");

        project.Name = request.Name.Trim();
        project.UpdatedAt = clock.UtcNow;
        await projects.UpdateAsync(project, ct);
        return Map(project);
    }

    private static ProjectDto Map(Project p) =>
        new(p.Id, p.ClientId, p.Name);
}

public sealed class TaskService(
    ITaskRepository tasks,
    IClientRepository clients,
    IProjectRepository projects,
    IClock clock)
{
    public async Task<IReadOnlyList<TaskDto>> ListAsync(
        Guid? clientId,
        bool? missingOnly,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        CancellationToken ct = default)
    {
        var list = await tasks.ListAsync(
            clientId, missingOnly, projectId, unassignedOnly, createdMonth, doneMonth, ct);
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

    public async Task<TaskFilterOptionsDto> GetFilterOptionsAsync(Guid? clientId, CancellationToken ct = default)
    {
        var (createdMonths, doneMonths) = await tasks.ListMonthFiltersAsync(clientId, ct);
        return new TaskFilterOptionsDto(createdMonths, doneMonths);
    }

    public async Task<TaskDto> UpdatePrepAsync(Guid id, UpdateTaskPrepRequest request, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        if (request.ProjectId is { } projectId)
        {
            var project = await projects.GetByIdAsync(projectId, ct)
                          ?? throw new NotFoundException("Project not found.");
            if (project.ClientId != task.ClientId)
                throw new DomainException("Project must belong to the same client as the task.");
        }

        task.ProjectId = request.ProjectId;
        task.Bill = request.Bill;
        task.BillableHours = request.BillableHours;
        task.NonBillableHours = request.NonBillableHours;
        task.InvoiceLabel = request.InvoiceLabel;
        task.Note = request.Note;
        task.UpdatedAt = clock.UtcNow;
        await tasks.UpdateAsync(task, ct);

        var client = await clients.GetByIdAsync(task.ClientId, ct);
        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;

        return Map(task, client?.Name ?? "Unknown", projectName);
    }

    private static TaskDto Map(WorkTask t, string clientName, string? projectName) =>
        new(
            t.Id, t.ClientId, clientName, t.ProjectId, projectName,
            t.Bill, t.BillableHours, t.NonBillableHours, t.InvoiceLabel, t.Note,
            t.ClickUpUrl, t.ClickUpTaskId, t.ClickUpParentId,
            t.ClickUpFolderId, t.ClickUpFolderName, t.ClickUpListId, t.ClickUpListName,
            t.Title, t.Description, t.ClickUpStatus, t.Tags,
            t.DateCreated, t.DueDate, t.DateDone, t.DateClosed,
            t.OrderIndex, t.EstimatedHours, t.ActualHours,
            NeedsAttention(t));

    private static bool NeedsAttention(WorkTask t) =>
        t.ProjectId is null
        || string.IsNullOrWhiteSpace(t.Bill)
        || (string.Equals(t.Bill, "yes", StringComparison.OrdinalIgnoreCase)
            && (t.BillableHours is null or 0))
        || string.IsNullOrWhiteSpace(t.InvoiceLabel);
}
