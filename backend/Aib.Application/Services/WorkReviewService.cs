using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

/// <summary>Pending / completed-work review queues and finalize/exclude actions (billing period attach is M6).</summary>
public sealed class WorkReviewService(
    ITaskRepository tasks,
    IClientRepository clients,
    IProjectRepository projects,
    ITimeEntryRepository timeEntries,
    IExternalTaskMappingRepository taskMappings,
    IMappingQueryRepository mappingQueries,
    IExternalConnectionRepository connections,
    IAgencyRepository agencies,
    AccessService access,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<IReadOnlyList<WorkItemReviewDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var accessible = await access.AccessibleClientIdsAsync(ct);
        var open = await tasks.ListByWorkStatusAsync(
            [WorkStatus.Pending, WorkStatus.InProgress, WorkStatus.Blocked], accessible, ct);
        return await MapManyAsync(open, ct);
    }

    public async Task<IReadOnlyList<WorkItemReviewDto>> ListCompletedAsync(CancellationToken ct = default)
    {
        var accessible = await access.AccessibleClientIdsAsync(ct);
        // Completed ClickUp work awaiting billing review (not yet finalized/invoiced/excluded).
        var completed = await tasks.ListByWorkStatusAsync([WorkStatus.Completed], accessible, ct);
        var review = completed.Where(t =>
            t.BillingStatus is BillingStatus.PendingReview or BillingStatus.Ready or BillingStatus.NotReady).ToList();
        return await MapManyAsync(review, ct);
    }

    public async Task<IReadOnlyList<WorkItemReviewDto>> ListFinalizedAsync(CancellationToken ct = default)
    {
        var accessible = await access.AccessibleClientIdsAsync(ct);
        var list = await tasks.ListByBillingStatusAsync(BillingStatus.Finalized, accessible, ct);
        return await MapManyAsync(list, ct);
    }

    public async Task<WorkItemReviewDto> FinalizeAsync(Guid taskId, FinalizeWorkRequest? request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var task = await tasks.GetByIdAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        if (task.BillingStatus is BillingStatus.Invoiced)
            throw new DomainException("Invoiced work cannot be finalized again.");
        if (task.BillingStatus is BillingStatus.Excluded)
            throw new DomainException("Excluded work must be reopened before finalizing.");

        task.BillingStatus = BillingStatus.Finalized;
        task.FinalizedAt = clock.UtcNow;
        task.FinalizedByUserId = currentUser.UserId;
        task.UpdatedAt = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
            task.Description = string.IsNullOrWhiteSpace(task.Description)
                ? request!.Notes
                : $"{task.Description}\n\n[finalize] {request.Notes}";
        await tasks.UpdateAsync(task, ct);
        return (await MapManyAsync([task], ct)).Single();
    }

    public async Task<WorkItemReviewDto> ExcludeAsync(Guid taskId, ExcludeWorkRequest? request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var task = await tasks.GetByIdAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        if (task.BillingStatus is BillingStatus.Invoiced)
            throw new DomainException("Invoiced work cannot be excluded.");

        task.BillingStatus = BillingStatus.Excluded;
        task.UpdatedAt = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(request?.Reason))
            task.Description = string.IsNullOrWhiteSpace(task.Description)
                ? $"[excluded] {request!.Reason}"
                : $"{task.Description}\n\n[excluded] {request.Reason}";
        await tasks.UpdateAsync(task, ct);
        return (await MapManyAsync([task], ct)).Single();
    }

    private async Task<IReadOnlyList<WorkItemReviewDto>> MapManyAsync(IReadOnlyList<WorkTask> list, CancellationToken ct)
    {
        var agency = await agencies.GetDefaultAsync(ct);
        var clickUp = agency is null ? null
            : (await connections.ListAsync(agency.Id, ct)).FirstOrDefault(c => c.ProviderType == "clickup");

        Dictionary<Guid, ExternalWorkItem>? workById = null;
        Dictionary<Guid, ExternalTaskMapping>? mapByTask = null;
        if (clickUp is not null)
        {
            var items = await mappingQueries.ListWorkItemsAsync(clickUp.Id, ct);
            var maps = await taskMappings.ListByConnectionAsync(clickUp.Id, MappingStatus.Confirmed, ct);
            mapByTask = maps.Where(m => m.TaskId is not null).ToDictionary(m => m.TaskId!.Value);
            var extIds = mapByTask.Values.Select(m => m.ExternalWorkItemId).ToHashSet();
            workById = items.Where(i => extIds.Contains(i.Id)).ToDictionary(i => i.Id);
        }

        var result = new List<WorkItemReviewDto>();
        foreach (var t in list)
        {
            var client = await clients.GetByIdAsync(t.ClientId, ct);
            Project? project = t.ProjectId is { } pid ? await projects.GetByIdAsync(pid, ct) : null;
            var entries = await timeEntries.ListByTaskAsync(t.Id, ct);
            var actual = entries.Sum(e => e.DurationMinutes);
            var billable = entries.Where(e => e.Billable).Sum(e => e.DurationMinutes);
            var amount = entries.Where(e => e.Billable).Sum(e => e.BillingAmount ?? 0);

            string? url = null; string? cuStatus = null;
            if (mapByTask is not null && mapByTask.TryGetValue(t.Id, out var m)
                && workById is not null && workById.TryGetValue(m.ExternalWorkItemId, out var w))
            {
                url = w.Url;
                cuStatus = w.StatusName;
            }

            result.Add(new WorkItemReviewDto(
                t.Id, t.ClientId, client?.Name ?? "?", t.ProjectId, project?.Name,
                t.Title, t.WorkStatus, t.BillingStatus, t.CompletedAt, t.EstimatedMinutes,
                actual, billable, amount == 0 ? null : amount, url, cuStatus));
        }
        return result;
    }
}
