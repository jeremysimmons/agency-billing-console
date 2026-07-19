using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class ProjectService(
    IProjectRepository projects,
    IClientRepository clients,
    AccessService access,
    IClock clock)
{
    public async Task<IReadOnlyList<ProjectDto>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        await access.EnsureCanViewClientAsync(clientId, ct);
        var list = await projects.ListByClientAsync(clientId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project not found.");
        await access.EnsureCanViewClientAsync(project.ClientId, ct);
        return Map(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Project name is required.");

        var client = await clients.GetByIdAsync(request.ClientId, ct)
                     ?? throw new NotFoundException("Client not found.");

        var now = clock.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = request.Name.Trim(),
            Code = request.Code?.Trim(),
            Description = request.Description,
            Status = request.Status ?? ProjectStatus.Active,
            BillingType = request.BillingType ?? BillingType.Hourly,
            HourlyRate = request.HourlyRate,
            FixedFee = request.FixedFee,
            BudgetMinutes = request.BudgetMinutes,
            BudgetAmount = request.BudgetAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await projects.InsertAsync(project, ct);
        return Map(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Project name is required.");

        project.Name = request.Name.Trim();
        project.Code = request.Code?.Trim();
        project.Description = request.Description;
        project.Status = request.Status;
        project.BillingType = request.BillingType;
        project.HourlyRate = request.HourlyRate;
        project.FixedFee = request.FixedFee;
        project.BudgetMinutes = request.BudgetMinutes;
        project.BudgetAmount = request.BudgetAmount;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Active = request.Active;
        project.UpdatedAt = clock.UtcNow;
        await projects.UpdateAsync(project, ct);
        return Map(project);
    }

    private static ProjectDto Map(Project p) =>
        new(p.Id, p.ClientId, p.Name, p.Code, p.Description, p.Status, p.BillingType,
            p.HourlyRate, p.FixedFee, p.BudgetMinutes, p.BudgetAmount, p.StartDate, p.EndDate, p.Active);
}
