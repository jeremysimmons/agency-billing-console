using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class TimeService(
    ITimeEntryRepository timeEntries,
    ITimeEntrySourceRepository sources,
    IExternalTimeEntryQueryRepository externalTime,
    IExternalConnectionRepository connections,
    IExternalTaskMappingRepository taskMappings,
    ITaskRepository tasks,
    IProjectRepository projects,
    IContractorRepository contractors,
    IAgencyRepository agencies,
    AccessService access,
    IClock clock)
{
    public async Task<IReadOnlyList<TimeEntryDto>> ListByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        await access.EnsureCanViewClientAsync(task.ClientId, ct);
        var list = await timeEntries.ListByTaskAsync(taskId, ct);
        var result = new List<TimeEntryDto>();
        foreach (var e in list)
            result.Add(await MapAsync(e, ct));
        return result;
    }

    public async Task<TimeEntryDto> CreateAsync(CreateTimeEntryRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (request.DurationMinutes < 0)
            throw new DomainException("Duration must be non-negative.");

        var task = await tasks.GetByIdAsync(request.TaskId, ct) ?? throw new NotFoundException("Task not found.");
        var contractor = await contractors.GetDefaultAsync(ct)
                         ?? throw new NotFoundException("Contractor not configured.");

        var now = clock.UtcNow;
        var rate = await ResolveRateAsync(request.HourlyRate, task, ct);
        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            ContractorId = contractor.Id,
            TaskId = request.TaskId,
            WorkDate = request.WorkDate,
            StartedAt = request.StartedAt,
            EndedAt = request.EndedAt,
            DurationMinutes = request.DurationMinutes,
            Description = request.Description,
            Billable = request.Billable ?? task.Billable,
            ApprovalStatus = ApprovalStatus.Draft,
            HourlyRate = rate,
            BillingAmount = ComputeAmount(rate, request.DurationMinutes),
            CreatedAt = now,
            UpdatedAt = now
        };
        await timeEntries.InsertAsync(entry, ct);
        return await MapAsync(entry, ct);
    }

    public async Task<TimeEntryDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var entry = await timeEntries.GetByIdAsync(id, ct) ?? throw new NotFoundException("Time entry not found.");
        var task = await tasks.GetByIdAsync(entry.TaskId, ct) ?? throw new NotFoundException("Task not found.");

        // Snapshot effective rate on approval.
        entry.HourlyRate = await ResolveRateAsync(entry.HourlyRate, task, ct);
        entry.BillingAmount = ComputeAmount(entry.HourlyRate, entry.DurationMinutes);
        entry.ApprovalStatus = ApprovalStatus.Approved;
        entry.UpdatedAt = clock.UtcNow;
        await timeEntries.UpdateAsync(entry, ct);
        return await MapAsync(entry, ct);
    }

    /// <summary>
    /// Create internal time entries from unlinked ClickUp time entries whose work items are Confirmed-mapped.
    /// Idempotent via <c>time_entry_source</c>.
    /// </summary>
    public async Task<SyncImportedTimeResult> SyncImportedAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var connection = connectionId is { } id
            ? await connections.GetByIdAsync(id, ct)
            : (await connections.ListAsync(agency.Id, ct)).FirstOrDefault(c => c.ProviderType == "clickup");
        if (connection is null) throw new NotFoundException("ClickUp connection not found.");

        var contractor = await contractors.GetDefaultAsync(ct)
                         ?? throw new NotFoundException("Contractor not configured.");
        var unlinked = await externalTime.ListUnlinkedForMappedTasksAsync(connection.Id, ct);
        var linked = 0; var skipped = 0; var failed = 0;
        var now = clock.UtcNow;

        foreach (var ext in unlinked)
        {
            try
            {
                if (await sources.GetByExternalIdAsync(ext.Id, ct) is not null)
                {
                    skipped++;
                    continue;
                }

                if (ext.ExternalWorkItemId is null)
                {
                    skipped++;
                    continue;
                }

                var mapping = await taskMappings.GetByWorkItemIdAsync(ext.ExternalWorkItemId.Value, ct);
                if (mapping is not { MappingStatus: MappingStatus.Confirmed, TaskId: { } taskId })
                {
                    skipped++;
                    continue;
                }

                var task = await tasks.GetByIdAsync(taskId, ct);
                if (task is null)
                {
                    skipped++;
                    continue;
                }

                var rate = await ResolveRateAsync(null, task, ct);
                var entry = new TimeEntry
                {
                    Id = Guid.NewGuid(),
                    ContractorId = contractor.Id,
                    TaskId = taskId,
                    WorkDate = ext.WorkDate,
                    StartedAt = ext.StartedAt,
                    EndedAt = ext.EndedAt,
                    DurationMinutes = ext.DurationMinutes,
                    Description = ext.Description,
                    Billable = ext.Billable ?? task.Billable,
                    ApprovalStatus = ApprovalStatus.Submitted,
                    HourlyRate = rate,
                    BillingAmount = ComputeAmount(rate, ext.DurationMinutes),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await timeEntries.InsertAsync(entry, ct);
                await sources.InsertAsync(new TimeEntrySource
                {
                    Id = Guid.NewGuid(),
                    TimeEntryId = entry.Id,
                    ExternalTimeEntryId = ext.Id,
                    ImportedDurationMinutes = ext.DurationMinutes,
                    ImportedAt = now
                }, ct);
                linked++;
            }
            catch
            {
                failed++;
            }
        }

        return new SyncImportedTimeResult(linked, skipped, failed);
    }

    private async Task<decimal?> ResolveRateAsync(decimal? entryRate, WorkTask task, CancellationToken ct)
    {
        if (entryRate is { } r) return r;
        if (task.HourlyRate is { } tr) return tr;
        if (task.ProjectId is { } pid)
        {
            var project = await projects.GetByIdAsync(pid, ct);
            if (project?.HourlyRate is { } pr) return pr;
        }
        var contractor = await contractors.GetDefaultAsync(ct);
        return contractor?.DefaultHourlyRate;
    }

    private static decimal? ComputeAmount(decimal? rate, int minutes) =>
        rate is { } r ? Math.Round(r * minutes / 60m, 2) : null;

    private async Task<TimeEntryDto> MapAsync(TimeEntry e, CancellationToken ct)
    {
        var src = await sources.GetByTimeEntryIdAsync(e.Id, ct);
        return new TimeEntryDto(
            e.Id, e.TaskId, e.ContractorId, e.WorkDate, e.DurationMinutes, e.Description,
            e.Billable, e.ApprovalStatus, e.HourlyRate, e.BillingAmount, e.StartedAt, e.EndedAt,
            FromImport: src is not null);
    }
}
