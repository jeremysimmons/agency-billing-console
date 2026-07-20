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
                (id, client_id, name, code, description, status, billing_type, active, created_at, updated_at)
            values
                ({p.Id}, {p.ClientId}, {p.Name}, {p.Code}, {p.Description}, 'Active', 'Hourly',
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
                active = {p.Active}, updated_at = {p.UpdatedAt}
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

    public async Task<IReadOnlyList<WorkTask>> ListAsync(Guid? clientId, bool? missingOnly, CancellationToken ct = default)
    {
        var sql = """
            select * from task
            where 1=1
            """;
        var parameters = new DynamicParameters();

        if (clientId is { } cid)
        {
            sql += " and client_id = @clientId";
            parameters.Add("clientId", cid);
        }

        if (missingOnly == true)
        {
            sql += """
                 and (
                    project_id is null
                    or bill is null
                    or (lower(bill) = 'yes' and (billable_hours is null or billable_hours = 0))
                    or invoice_label is null or trim(invoice_label) = ''
                 )
                """;
        }

        sql += " order by date_done desc nulls last, date_created desc nulls last, title";

        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into task
                (id, client_id, project_id, bill, billable_hours, non_billable_hours, invoice_label, note,
                 clickup_url, clickup_task_id, clickup_parent_id, clickup_folder_id, clickup_folder_name,
                 clickup_list_id, clickup_list_name, title, description, clickup_status, tags,
                 date_created, due_date, date_done, date_closed, order_index, estimated_hours, actual_hours,
                 created_at, updated_at)
            values
                ({t.Id}, {t.ClientId}, {t.ProjectId}, {t.Bill}, {t.BillableHours}, {t.NonBillableHours},
                 {t.InvoiceLabel}, {t.Note}, {t.ClickUpUrl}, {t.ClickUpTaskId}, {t.ClickUpParentId},
                 {t.ClickUpFolderId}, {t.ClickUpFolderName}, {t.ClickUpListId}, {t.ClickUpListName},
                 {t.Title}, {t.Description}, {t.ClickUpStatus}, {t.Tags}, {t.DateCreated}, {t.DueDate},
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
                title = {t.Title}, description = {t.Description}, clickup_status = {t.ClickUpStatus}, tags = {t.Tags},
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
                title = {t.Title}, description = {t.Description}, clickup_status = {t.ClickUpStatus}, tags = {t.Tags},
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
