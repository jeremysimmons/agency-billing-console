using Aib.Application.Abstractions;
using Aib.Domain;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class TimeEntryRepository(IDbConnectionFactory factory) : ITimeEntryRepository
{
    public async Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from time_entry where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TimeEntry>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from time_entry where task_id = {taskId} order by work_date, started_at nulls last");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<TimeEntry>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select te.* from time_entry te
            join task t on t.id = te.task_id
            where t.client_id = {clientId}
            order by te.work_date desc
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<TimeEntry>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> SumDurationMinutesAsync(Guid taskId, bool directOnly, CancellationToken ct = default)
    {
        Builder b;
        if (directOnly)
        {
            b = SimpleBuilder.Create($"select coalesce(sum(duration_minutes), 0) from time_entry where task_id = {taskId}");
        }
        else
        {
            b = SimpleBuilder.Create($"""
                with recursive tree as (
                    select id from task where id = {taskId}
                    union all
                    select t.id from task t join tree p on t.parent_task_id = p.id
                )
                select coalesce(sum(te.duration_minutes), 0)
                from time_entry te
                join tree on tree.id = te.task_id
                """);
        }
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(TimeEntry e, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into time_entry
                (id, contractor_id, task_id, billing_period_id, work_date, started_at, ended_at,
                 duration_minutes, description, billable, approval_status, hourly_rate, billing_amount,
                 invoice_line_id, created_at, updated_at)
            values
                ({e.Id}, {e.ContractorId}, {e.TaskId}, {e.BillingPeriodId}, {e.WorkDate}, {e.StartedAt}, {e.EndedAt},
                 {e.DurationMinutes}, {e.Description}, {e.Billable}, {e.ApprovalStatus}, {e.HourlyRate}, {e.BillingAmount},
                 {e.InvoiceLineId}, {e.CreatedAt}, {e.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return e.Id;
    }

    public async Task UpdateAsync(TimeEntry e, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            update time_entry set
                work_date = {e.WorkDate}, started_at = {e.StartedAt}, ended_at = {e.EndedAt},
                duration_minutes = {e.DurationMinutes}, description = {e.Description}, billable = {e.Billable},
                approval_status = {e.ApprovalStatus}, hourly_rate = {e.HourlyRate}, billing_amount = {e.BillingAmount},
                billing_period_id = {e.BillingPeriodId}, invoice_line_id = {e.InvoiceLineId}, updated_at = {e.UpdatedAt}
            where id = {e.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class TimeEntrySourceRepository(IDbConnectionFactory factory) : ITimeEntrySourceRepository
{
    public async Task<TimeEntrySource?> GetByExternalIdAsync(Guid externalTimeEntryId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from time_entry_source where external_time_entry_id = {externalTimeEntryId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TimeEntrySource>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<TimeEntrySource?> GetByTimeEntryIdAsync(Guid timeEntryId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from time_entry_source where time_entry_id = {timeEntryId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TimeEntrySource>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task InsertAsync(TimeEntrySource s, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into time_entry_source (id, time_entry_id, external_time_entry_id, imported_duration_minutes, imported_at)
            values ({s.Id}, {s.TimeEntryId}, {s.ExternalTimeEntryId}, {s.ImportedDurationMinutes}, {s.ImportedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class ExternalTimeEntryQueryRepository(IDbConnectionFactory factory) : IExternalTimeEntryQueryRepository
{
    public async Task<ExternalTimeEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_time_entry where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalTimeEntry>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExternalTimeEntry>> ListUnlinkedForMappedTasksAsync(Guid connectionId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select e.*
            from external_time_entry e
            join external_work_item w on w.id = e.external_work_item_id
            join external_task_mapping m on m.external_work_item_id = w.id
            where e.external_connection_id = {connectionId}
              and m.mapping_status = {((int)MappingStatus.Confirmed).ToString()}
              and m.task_id is not null
              and not exists (
                  select 1 from time_entry_source s where s.external_time_entry_id = e.id
              )
            order by e.work_date
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalTimeEntry>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }
}
