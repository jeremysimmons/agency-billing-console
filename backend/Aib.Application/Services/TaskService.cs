using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class TaskService(
    ITaskRepository tasks,
    IClientRepository clients,
    IProjectRepository projects,
    AccessService access,
    IClock clock)
{
    public async Task<IReadOnlyList<TaskDto>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        await access.EnsureCanViewClientAsync(clientId, ct);
        var list = await tasks.ListByClientAsync(clientId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        await access.EnsureCanViewClientAsync(task.ClientId, ct);
        return Map(task);
    }

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("Task title is required.");

        _ = await clients.GetByIdAsync(request.ClientId, ct)
            ?? throw new NotFoundException("Client not found.");

        var now = clock.UtcNow;
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            ProjectId = request.ProjectId,
            ParentTaskId = request.ParentTaskId,
            Title = request.Title.Trim(),
            Description = request.Description,
            WorkStatus = WorkStatus.Pending,
            BillingStatus = BillingStatus.NotReady,
            BillingType = request.BillingType ?? BillingType.Hourly,
            Billable = request.Billable ?? true,
            HourlyRate = request.HourlyRate,
            FixedFee = request.FixedFee,
            EstimatedMinutes = request.EstimatedMinutes,
            EstimateRollupMode = request.EstimateRollupMode ?? RollupMode.Direct,
            ActualRollupMode = request.ActualRollupMode ?? RollupMode.DirectAndChildren,
            BillingRollupMode = request.BillingRollupMode ?? BillingRollupMode.Task,
            DueDate = request.DueDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        await ValidateHierarchyAsync(task, ct);
        await tasks.InsertAsync(task, ct);
        return Map(task);
    }

    public async Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var task = await tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task not found.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("Task title is required.");

        task.ProjectId = request.ProjectId;
        task.ParentTaskId = request.ParentTaskId;
        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.WorkStatus = request.WorkStatus;
        task.BillingType = request.BillingType;
        task.Billable = request.Billable;
        task.HourlyRate = request.HourlyRate;
        task.FixedFee = request.FixedFee;
        task.EstimatedMinutes = request.EstimatedMinutes;
        task.EstimateRollupMode = request.EstimateRollupMode;
        task.ActualRollupMode = request.ActualRollupMode;
        task.BillingRollupMode = request.BillingRollupMode;
        task.DueDate = request.DueDate;
        task.SortOrder = request.SortOrder;

        // A completed task is not automatically finalized for billing.
        if (task.WorkStatus == WorkStatus.Completed && task.CompletedAt is null)
            task.CompletedAt = clock.UtcNow;
        if (task.WorkStatus != WorkStatus.Completed)
            task.CompletedAt = null;

        task.UpdatedAt = clock.UtcNow;

        await ValidateHierarchyAsync(task, ct);
        await tasks.UpdateAsync(task, ct);
        return Map(task);
    }

    private async Task ValidateHierarchyAsync(WorkTask task, CancellationToken ct)
    {
        Project? project = null;
        if (task.ProjectId is { } projectId)
        {
            project = await projects.GetByIdAsync(projectId, ct)
                      ?? throw new NotFoundException("Project not found.");
        }

        WorkTask? parent = null;
        if (task.ParentTaskId is { } parentId)
        {
            parent = await tasks.GetByIdAsync(parentId, ct)
                     ?? throw new NotFoundException("Parent task not found.");

            var ancestors = await tasks.GetAncestorIdsAsync(parentId, ct);
            var ancestorSet = new HashSet<Guid>(ancestors) { parentId };
            TaskRules.EnsureNoCycle(task.Id, task.ParentTaskId, ancestorSet);
        }

        TaskRules.ValidatePlacement(task, parent, project);
    }

    private static TaskDto Map(WorkTask t) =>
        new(t.Id, t.ClientId, t.ProjectId, t.ParentTaskId, t.Title, t.Description,
            t.WorkStatus, t.BillingStatus, t.BillingType, t.Billable, t.HourlyRate, t.FixedFee,
            t.EstimatedMinutes, t.EstimateRollupMode, t.ActualRollupMode, t.BillingRollupMode,
            t.DueDate, t.CompletedAt, t.FinalizedAt, t.SortOrder);
}
