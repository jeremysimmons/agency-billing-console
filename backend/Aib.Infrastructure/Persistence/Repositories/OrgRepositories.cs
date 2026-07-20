using Aib.Application.Abstractions;
using Aib.Domain;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class AgencyRepository(IDbConnectionFactory factory) : IAgencyRepository
{
    public async Task<Agency?> GetDefaultAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Agency>(
            new CommandDefinition("select * from agency order by created_at limit 1", cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(Agency a, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into agency
                (id, name, billing_email, billing_address, currency, payment_terms_days, active, created_at, updated_at)
            values
                ({a.Id}, {a.Name}, {a.BillingEmail}, {a.BillingAddress}, {a.Currency}, {a.PaymentTermsDays},
                 {a.Active}, {a.CreatedAt}, {a.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return a.Id;
    }

    public async Task UpdateAsync(Agency a, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update agency set
                name = {a.Name},
                billing_email = {a.BillingEmail},
                billing_address = {a.BillingAddress},
                currency = {a.Currency},
                payment_terms_days = {a.PaymentTermsDays},
                active = {a.Active},
                updated_at = {a.UpdatedAt}
            where id = {a.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class ContractorRepository(IDbConnectionFactory factory) : IContractorRepository
{
    public async Task<Contractor?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from contractor where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Contractor>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Contractor?> GetDefaultAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Contractor>(
            new CommandDefinition("select * from contractor order by created_at limit 1", cancellationToken: ct));
    }

    public async Task<Contractor?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from contractor where lower(email) = lower({email})");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Contractor>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(Contractor c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into contractor (id, name, email, default_hourly_rate, active, created_at, updated_at)
            values ({c.Id}, {c.Name}, {c.Email}, {c.DefaultHourlyRate}, {c.Active}, {c.CreatedAt}, {c.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return c.Id;
    }
}

public sealed class ClientRepository(IDbConnectionFactory factory) : IClientRepository
{
    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Client>> ListAsync(Guid agencyId, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where agency_id = {agencyId}");
        if (restrictToClientIds is not null)
        {
            if (restrictToClientIds.Count == 0)
                return [];
            builder.AppendNewLine($"and id in {restrictToClientIds}");
        }
        builder.AppendNewLine($"order by name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(Client c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into client (id, agency_id, name, code, original_name, description, status, active, created_at, updated_at)
            values ({c.Id}, {c.AgencyId}, {c.Name}, {c.Code}, {c.OriginalName}, {c.Description}, {c.Status}, {c.Active}, {c.CreatedAt}, {c.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return c.Id;
    }

    public async Task UpdateAsync(Client c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update client set name = {c.Name}, code = {c.Code}, original_name = {c.OriginalName}, description = {c.Description},
                status = {c.Status}, active = {c.Active}, updated_at = {c.UpdatedAt}
            where id = {c.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deleteClient = SimpleBuilder.Create($"delete from client where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(deleteClient.Sql, deleteClient.Parameters, cancellationToken: ct));
    }
}

public sealed class ProjectRepository(IDbConnectionFactory factory) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from project where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Project>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Project>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from project where client_id = {clientId} order by name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Project>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(Project p, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into project
                (id, client_id, name, code, description, status, billing_type, hourly_rate, fixed_fee,
                 budget_minutes, budget_amount, start_date, end_date, active, created_at, updated_at)
            values
                ({p.Id}, {p.ClientId}, {p.Name}, {p.Code}, {p.Description}, {p.Status}, {p.BillingType},
                 {p.HourlyRate}, {p.FixedFee}, {p.BudgetMinutes}, {p.BudgetAmount}, {p.StartDate}, {p.EndDate},
                 {p.Active}, {p.CreatedAt}, {p.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return p.Id;
    }

    public async Task UpdateAsync(Project p, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update project set name = {p.Name}, code = {p.Code}, description = {p.Description},
                status = {p.Status}, billing_type = {p.BillingType}, hourly_rate = {p.HourlyRate},
                fixed_fee = {p.FixedFee}, budget_minutes = {p.BudgetMinutes}, budget_amount = {p.BudgetAmount},
                start_date = {p.StartDate}, end_date = {p.EndDate}, active = {p.Active}, updated_at = {p.UpdatedAt}
            where id = {p.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class TaskRepository(IDbConnectionFactory factory) : ITaskRepository
{
    public async Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from task where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<WorkTask>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from task where client_id = {clientId} order by sort_order, created_at");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WorkTask>> ListByWorkStatusAsync(IReadOnlyCollection<WorkStatus> statuses, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default)
    {
        var statusValues = statuses.Select(s => ((int)s).ToString()).ToArray();
        var builder = SimpleBuilder.Create($"select * from task where work_status in {statusValues}");
        if (restrictToClientIds is not null)
        {
            if (restrictToClientIds.Count == 0) return [];
            builder.Append($" and client_id in {restrictToClientIds}");
        }
        builder.AppendNewLine($"order by completed_at nulls last, updated_at desc");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WorkTask>> ListByBillingStatusAsync(BillingStatus status, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default)
    {
        var statusVal = ((int)status).ToString();
        var builder = SimpleBuilder.Create($"select * from task where billing_status = {statusVal}");
        if (restrictToClientIds is not null)
        {
            if (restrictToClientIds.Count == 0) return [];
            builder.Append($" and client_id in {restrictToClientIds}");
        }
        builder.AppendNewLine($"order by completed_at nulls last, updated_at desc");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into task
                (id, client_id, project_id, parent_task_id, title, description, work_status, billing_status,
                 billing_type, billable, hourly_rate, fixed_fee, estimated_minutes, estimate_rollup_mode,
                 actual_rollup_mode, billing_rollup_mode, due_date, completed_at, finalized_at,
                 finalized_by_user_id, sort_order, created_at, updated_at)
            values
                ({t.Id}, {t.ClientId}, {t.ProjectId}, {t.ParentTaskId}, {t.Title}, {t.Description}, {t.WorkStatus},
                 {t.BillingStatus}, {t.BillingType}, {t.Billable}, {t.HourlyRate}, {t.FixedFee}, {t.EstimatedMinutes},
                 {t.EstimateRollupMode}, {t.ActualRollupMode}, {t.BillingRollupMode}, {t.DueDate}, {t.CompletedAt},
                 {t.FinalizedAt}, {t.FinalizedByUserId}, {t.SortOrder}, {t.CreatedAt}, {t.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return t.Id;
    }

    public async Task UpdateAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update task set project_id = {t.ProjectId}, parent_task_id = {t.ParentTaskId}, title = {t.Title},
                description = {t.Description}, work_status = {t.WorkStatus}, billing_status = {t.BillingStatus},
                billing_type = {t.BillingType}, billable = {t.Billable}, hourly_rate = {t.HourlyRate},
                fixed_fee = {t.FixedFee}, estimated_minutes = {t.EstimatedMinutes},
                estimate_rollup_mode = {t.EstimateRollupMode}, actual_rollup_mode = {t.ActualRollupMode},
                billing_rollup_mode = {t.BillingRollupMode}, due_date = {t.DueDate}, completed_at = {t.CompletedAt},
                finalized_at = {t.FinalizedAt}, finalized_by_user_id = {t.FinalizedByUserId},
                sort_order = {t.SortOrder}, updated_at = {t.UpdatedAt}
            where id = {t.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid taskId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            with recursive ancestors as (
                select parent_task_id as id from task where id = {taskId}
                union all
                select t.parent_task_id from task t
                join ancestors a on t.id = a.id
                where t.parent_task_id is not null
            )
            select id from ancestors where id is not null
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Guid>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WorkTask>> GetSubtreeAsync(Guid rootTaskId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            with recursive tree as (
                select * from task where id = {rootTaskId}
                union all
                select t.* from task t
                join tree p on t.parent_task_id = p.id
            )
            select * from tree
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }
}
