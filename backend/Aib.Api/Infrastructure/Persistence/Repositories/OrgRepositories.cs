using Aib.Application.Abstractions;
using Aib.Application.Contracts;
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
                (id, name, billing_email, billing_address, currency, payment_terms_days, active,
                 last_clickup_sync_at, last_clickup_sync_summary, created_at, updated_at)
            values
                ({a.Id}, {a.Name}, {a.BillingEmail}, {a.BillingAddress}, {a.Currency}, {a.PaymentTermsDays},
                 {a.Active}, {a.LastClickUpSyncAt}, {a.LastClickUpSyncSummary}, {a.CreatedAt}, {a.UpdatedAt})
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
                last_clickup_sync_at = {a.LastClickUpSyncAt},
                last_clickup_sync_summary = {a.LastClickUpSyncSummary},
                updated_at = {a.UpdatedAt}
            where id = {a.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpdateSyncSummaryAsync(Guid id, DateTimeOffset syncedAt, string summary, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update agency set
                last_clickup_sync_at = {syncedAt},
                last_clickup_sync_summary = {summary},
                updated_at = {syncedAt}
            where id = {id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
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

    public async Task<Client?> GetByClickUpFolderIdAsync(string folderId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where clickup_folder_id = {folderId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Client>> ListAsync(Guid agencyId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where agency_id = {agencyId} order by name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(Client c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into client
                (id, agency_id, name, code, original_name, clickup_folder_id, description, status, active, created_at, updated_at)
            values
                ({c.Id}, {c.AgencyId}, {c.Name}, {c.Code}, {c.OriginalName}, {c.ClickUpFolderId}, {c.Description},
                 {c.Status}, {c.Active}, {c.CreatedAt}, {c.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return c.Id;
    }

    public async Task UpdateAsync(Client c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update client set name = {c.Name}, code = {c.Code}, original_name = {c.OriginalName},
                clickup_folder_id = {c.ClickUpFolderId}, description = {c.Description},
                status = {c.Status}, active = {c.Active}, updated_at = {c.UpdatedAt}
            where id = {c.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"delete from client where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("delete from client", cancellationToken: ct));
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
            insert into project (id, client_id, name, created_at, updated_at)
            values ({p.Id}, {p.ClientId}, {p.Name}, {p.CreatedAt}, {p.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return p.Id;
    }

    public async Task UpdateAsync(Project p, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update project set name = {p.Name}, updated_at = {p.UpdatedAt}
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

    public async Task<WorkTask?> GetByClickUpUrlAsync(string url, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from task where clickup_url = {url}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<WorkTask?> GetByClickUpTaskIdAsync(string taskId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from task where clickup_task_id = {taskId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<WorkTask>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid? clientId,
        bool? missingOnly,
        string? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        CancellationToken ct = default)
    {
        var sql = """
            select * from task
            where 1=1
            """;
        var parameters = new DynamicParameters();
        ApplyTaskFilters(ref sql, parameters, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses);

        sql += " order by date_done asc nulls last, date_created asc nulls last, title";

        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(IReadOnlyList<TaskClientCountRow> ByClient, IReadOnlyList<TaskMonthCountRow> ByDoneMonth)> GetSummaryAsync(
        Guid? clientId,
        bool? missingOnly,
        string? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        CancellationToken ct = default)
    {
        const string missingSql = """
            t.bill is null
            or (lower(t.bill) = 'yes' and (t.billable_hours is null or t.billable_hours = 0))
            or t.invoice_label is null or trim(t.invoice_label) = ''
            """;
        const string uninvoicedSql = "t.invoice_label is null or trim(t.invoice_label) = ''";

        var clientSql = $"""
            select
                t.client_id as ClientId,
                c.name as ClientName,
                count(*)::int as TaskCount,
                count(*) filter (where {missingSql})::int as MissingCount,
                count(*) filter (where {uninvoicedSql})::int as UninvoicedCount
            from task t
            join client c on c.id = t.client_id
            where 1=1
            """;
        var clientParams = new DynamicParameters();
        ApplyTaskFilters(ref clientSql, clientParams, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses, "t.");
        clientSql += """
             group by t.client_id, c.name
             order by c.name asc
            """;

        var monthSql = $"""
            select
                to_char(t.date_done, 'YYYY-MM') as Month,
                count(*)::int as TaskCount,
                count(*) filter (where {missingSql})::int as MissingCount,
                count(*) filter (where {uninvoicedSql})::int as UninvoicedCount
            from task t
            where t.date_done is not null
            """;
        var monthParams = new DynamicParameters();
        ApplyTaskFilters(ref monthSql, monthParams, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses, "t.");
        monthSql += """
             group by to_char(t.date_done, 'YYYY-MM')
             order by month asc
            """;

        using var conn = await factory.OpenAsync(ct);
        var byClient = (await conn.QueryAsync<TaskClientCountRow>(
            new CommandDefinition(clientSql, clientParams, cancellationToken: ct))).ToList();
        var byDoneMonth = (await conn.QueryAsync<TaskMonthCountRow>(
            new CommandDefinition(monthSql, monthParams, cancellationToken: ct))).ToList();
        return (byClient, byDoneMonth);
    }

    private static void ApplyTaskFilters(
        ref string sql,
        DynamicParameters parameters,
        Guid? clientId,
        bool? missingOnly,
        string? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string prefix = "")
    {
        if (clientId is { } cid)
        {
            sql += $" and {prefix}client_id = @clientId";
            parameters.Add("clientId", cid);
        }

        if (projectId is { } pid)
        {
            sql += $" and {prefix}project_id = @projectId";
            parameters.Add("projectId", pid);
        }
        else if (unassignedOnly == true)
        {
            sql += $" and {prefix}project_id is null";
        }

        if (!string.IsNullOrWhiteSpace(createdMonth))
        {
            sql += $" and {prefix}date_created is not null and to_char({prefix}date_created, 'YYYY-MM') = @createdMonth";
            parameters.Add("createdMonth", createdMonth);
        }

        if (!string.IsNullOrWhiteSpace(doneMonth))
        {
            sql += $" and {prefix}date_done is not null and to_char({prefix}date_done, 'YYYY-MM') = @doneMonth";
            parameters.Add("doneMonth", doneMonth);
        }

        if (statuses is { Count: > 0 })
        {
            sql += $" and {prefix}clickup_status = any(@statuses)";
            parameters.Add("statuses", statuses);
        }

        if (missingOnly == true)
        {
            sql += $"""
                 and (
                    {prefix}bill is null
                    or (lower({prefix}bill) = 'yes' and ({prefix}billable_hours is null or {prefix}billable_hours = 0))
                    or {prefix}invoice_label is null or trim({prefix}invoice_label) = ''
                 )
                """;
        }

        if (string.Equals(invoiced, "yes", StringComparison.OrdinalIgnoreCase))
        {
            sql += $" and ({prefix}invoice_label is not null and trim({prefix}invoice_label) <> '')";
        }
        else if (!string.Equals(invoiced, "all", StringComparison.OrdinalIgnoreCase))
        {
            sql += $" and ({prefix}invoice_label is null or trim({prefix}invoice_label) = '')";
        }
    }

    public async Task<(IReadOnlyList<string> CreatedMonths, IReadOnlyList<string> DoneMonths, IReadOnlyList<string> Statuses)> ListFilterOptionsAsync(
        Guid? clientId, CancellationToken ct = default)
    {
        var clientClause = clientId is { } cid ? " and client_id = @clientId" : string.Empty;
        var parameters = new DynamicParameters();
        if (clientId is { } clientIdValue)
            parameters.Add("clientId", clientIdValue);

        var createdSql = $"""
            select distinct to_char(date_created, 'YYYY-MM') as month
            from task
            where date_created is not null{clientClause}
            order by month asc
            """;
        var doneSql = $"""
            select distinct to_char(date_done, 'YYYY-MM') as month
            from task
            where date_done is not null{clientClause}
            order by month asc
            """;
        var statusSql = $"""
            select clickup_status as status
            from task
            where clickup_status is not null and trim(clickup_status) <> ''{clientClause}
            group by clickup_status
            order by min(clickup_status_order) nulls last, clickup_status asc
            """;

        using var conn = await factory.OpenAsync(ct);
        var created = (await conn.QueryAsync<string>(new CommandDefinition(createdSql, parameters, cancellationToken: ct))).ToList();
        var done = (await conn.QueryAsync<string>(new CommandDefinition(doneSql, parameters, cancellationToken: ct))).ToList();
        var statuses = (await conn.QueryAsync<string>(new CommandDefinition(statusSql, parameters, cancellationToken: ct))).ToList();
        return (created, done, statuses);
    }

    public async Task<Guid> InsertAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into task
                (id, client_id, project_id, bill, billable_hours, non_billable_hours, invoice_label, note,
                 clickup_url, clickup_task_id, clickup_parent_id, clickup_folder_id, clickup_folder_name,
                 clickup_list_id, clickup_list_name, title, description, clickup_status, clickup_status_order, tags,
                 date_created, due_date, date_done, date_closed, order_index, estimated_hours, actual_hours,
                 created_at, updated_at)
            values
                ({t.Id}, {t.ClientId}, {t.ProjectId}, {t.Bill}, {t.BillableHours}, {t.NonBillableHours},
                 {t.InvoiceLabel}, {t.Note}, {t.ClickUpUrl}, {t.ClickUpTaskId}, {t.ClickUpParentId},
                 {t.ClickUpFolderId}, {t.ClickUpFolderName}, {t.ClickUpListId}, {t.ClickUpListName},
                 {t.Title}, {t.Description}, {t.ClickUpStatus}, {t.ClickUpStatusOrder}, {t.Tags}, {t.DateCreated}, {t.DueDate},
                 {t.DateDone}, {t.DateClosed}, {t.OrderIndex}, {t.EstimatedHours}, {t.ActualHours},
                 {t.CreatedAt}, {t.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return t.Id;
    }

    public async Task UpdateAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update task set
                client_id = {t.ClientId}, project_id = {t.ProjectId},
                bill = {t.Bill}, billable_hours = {t.BillableHours}, non_billable_hours = {t.NonBillableHours},
                invoice_label = {t.InvoiceLabel}, note = {t.Note},
                clickup_url = {t.ClickUpUrl}, clickup_task_id = {t.ClickUpTaskId}, clickup_parent_id = {t.ClickUpParentId},
                clickup_folder_id = {t.ClickUpFolderId}, clickup_folder_name = {t.ClickUpFolderName},
                clickup_list_id = {t.ClickUpListId}, clickup_list_name = {t.ClickUpListName},
                title = {t.Title}, description = {t.Description}, clickup_status = {t.ClickUpStatus},
                clickup_status_order = {t.ClickUpStatusOrder}, tags = {t.Tags},
                date_created = {t.DateCreated}, due_date = {t.DueDate}, date_done = {t.DateDone},
                date_closed = {t.DateClosed}, order_index = {t.OrderIndex},
                estimated_hours = {t.EstimatedHours}, actual_hours = {t.ActualHours},
                updated_at = {t.UpdatedAt}
            where id = {t.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpdateApiFieldsAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update task set
                client_id = {t.ClientId},
                clickup_url = {t.ClickUpUrl}, clickup_task_id = {t.ClickUpTaskId}, clickup_parent_id = {t.ClickUpParentId},
                clickup_folder_id = {t.ClickUpFolderId}, clickup_folder_name = {t.ClickUpFolderName},
                clickup_list_id = {t.ClickUpListId}, clickup_list_name = {t.ClickUpListName},
                title = {t.Title}, description = {t.Description}, clickup_status = {t.ClickUpStatus},
                clickup_status_order = {t.ClickUpStatusOrder}, tags = {t.Tags},
                date_created = {t.DateCreated}, due_date = {t.DueDate}, date_done = {t.DateDone},
                date_closed = {t.DateClosed}, order_index = {t.OrderIndex},
                estimated_hours = {t.EstimatedHours}, actual_hours = {t.ActualHours},
                updated_at = {t.UpdatedAt}
            where id = {t.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class ClickUpContainerRepository(IDbConnectionFactory factory) : IClickUpContainerRepository
{
    public async Task UpsertManyAsync(IReadOnlyList<ClickUpContainer> containers, CancellationToken ct = default)
    {
        if (containers.Count == 0) return;
        using var conn = await factory.OpenAsync(ct);
        foreach (var c in containers)
        {
            var builder = SimpleBuilder.Create($"""
                insert into clickup_container
                    (id, container_type, external_id, name, parent_type, parent_external_id, updated_at)
                values
                    ({c.Id}, {c.ContainerType}, {c.ExternalId}, {c.Name}, {c.ParentType}, {c.ParentExternalId}, {c.UpdatedAt})
                on conflict (external_id) do update set
                    container_type = excluded.container_type,
                    name = excluded.name,
                    parent_type = excluded.parent_type,
                    parent_external_id = excluded.parent_external_id,
                    updated_at = excluded.updated_at
                """);
            await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        }
    }

    public async Task<IReadOnlyList<ClickUpContainer>> ListAllAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ClickUpContainer>(
            new CommandDefinition("select * from clickup_container order by container_type, name", cancellationToken: ct));
        return rows.ToList();
    }
}
