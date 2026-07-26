using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;
using Aib.Infrastructure.Persistence;
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
                ui_preferences = cast({a.UiPreferences} as jsonb),
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

    public async Task<Client?> GetByNameAsync(Guid agencyId, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var builder = SimpleBuilder.Create($"""
            select * from client
            where agency_id = {agencyId} and lower(trim(name)) = lower({trimmed})
            limit 1
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Client?> GetByClickUpFolderIdAsync(string folderId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where clickup_folder_id = {folderId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Client>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Client?> GetByClickUpListIdAsync(string listId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from client where clickup_list_id = {listId}");
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
                (id, agency_id, name, code, original_name, clickup_folder_id, clickup_list_id, description, status, active,
                 bill_field_available, bill_custom_field_id, bill_yes_option_id, bill_no_option_id, bill_field_checked_at,
                 created_at, updated_at)
            values
                ({c.Id}, {c.AgencyId}, {c.Name}, {c.Code}, {c.OriginalName}, {c.ClickUpFolderId}, {c.ClickUpListId}, {c.Description},
                 {c.Status}, {c.Active},
                 {c.BillFieldAvailable}, {c.BillCustomFieldId}, {c.BillYesOptionId}, {c.BillNoOptionId}, {c.BillFieldCheckedAt},
                 {c.CreatedAt}, {c.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return c.Id;
    }

    public async Task UpdateAsync(Client c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update client set name = {c.Name}, code = {c.Code}, original_name = {c.OriginalName},
                clickup_folder_id = {c.ClickUpFolderId}, clickup_list_id = {c.ClickUpListId}, description = {c.Description},
                status = {c.Status}, active = {c.Active},
                bill_field_available = {c.BillFieldAvailable},
                bill_custom_field_id = {c.BillCustomFieldId},
                bill_yes_option_id = {c.BillYesOptionId},
                bill_no_option_id = {c.BillNoOptionId},
                bill_field_checked_at = {c.BillFieldCheckedAt},
                updated_at = {c.UpdatedAt}
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

    public async Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Project>(new CommandDefinition(
            """
            select p.*
            from project p
            inner join client c on c.id = p.client_id
            order by c.name, p.name
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Project>> ListByClientAsync(Guid clientId, bool includeShared = false, CancellationToken ct = default)
    {
        var sharedName = SharedClients.Name;
        var builder = includeShared
            ? SimpleBuilder.Create($"""
                select p.*
                from project p
                inner join client c on c.id = p.client_id
                where p.client_id = {clientId}
                   or lower(trim(c.name)) = lower({sharedName})
                order by case when p.client_id = {clientId} then 0 else 1 end, p.name
                """)
            : SimpleBuilder.Create($"select * from project where client_id = {clientId} order by name");
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
            update project
            set name = {p.Name}, client_id = {p.ClientId}, updated_at = {p.UpdatedAt}
            where id = {p.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class InvoiceRepository(IDbConnectionFactory factory) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from invoice where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Invoice>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Invoice?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var builder = SimpleBuilder.Create($"select * from invoice where lower(trim(name)) = lower({trimmed})");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Invoice>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Invoice?> GetDefaultAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Invoice>(new CommandDefinition(
            """
            select * from invoice
            where is_default and lower(trim(status)) = 'preparing'
            order by sort_order, name
            limit 1
            """, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Invoice>(new CommandDefinition(
            """
            select * from invoice
            order by sort_order, name
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> GetNextSortOrderAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "select coalesce(max(sort_order), -1) + 1 from invoice",
            cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(Invoice invoice, CancellationToken ct = default)
    {
        var status = invoice.Status.Value;
        var builder = SimpleBuilder.Create($"""
            insert into invoice (id, name, status, sort_order, is_default, rate, created_at, updated_at)
            values ({invoice.Id}, {invoice.Name}, {status}, {invoice.SortOrder}, {invoice.IsDefault}, {invoice.Rate}, {invoice.CreatedAt}, {invoice.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return invoice.Id;
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        var status = invoice.Status.Value;
        var builder = SimpleBuilder.Create($"""
            update invoice
            set name = {invoice.Name}, status = {status}, sort_order = {invoice.SortOrder},
                is_default = {invoice.IsDefault}, rate = {invoice.Rate}, updated_at = {invoice.UpdatedAt}
            where id = {invoice.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task ClearDefaultsAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "update invoice set is_default = false where is_default",
            cancellationToken: ct));
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedIds, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        using var tx = conn.BeginTransaction();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var id = orderedIds[i];
            var sortOrder = i;
            var builder = SimpleBuilder.Create($"""
                update invoice
                set sort_order = {sortOrder}, updated_at = {updatedAt}
                where id = {id}
                """);
            await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, transaction: tx, cancellationToken: ct));
        }
        tx.Commit();
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
        IReadOnlyList<string>? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId,
        string? clickUpFolderId,
        string? clickUpSpaceId,
        string? invoiceLabel,
        CancellationToken ct = default)
    {
        var sql = """
            select * from task
            where 1=1
            """;
        var parameters = new DynamicParameters();
        ApplyTaskFilters(ref sql, parameters, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            clickUpListId, clickUpFolderId, clickUpSpaceId, invoiceLabel);

        sql += " order by date_done asc nulls last, date_created asc nulls last, title";

        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(IReadOnlyList<TaskClientCountRow> ByClient, IReadOnlyList<TaskMonthCountRow> ByDoneMonth)> GetSummaryAsync(
        Guid? clientId,
        bool? missingOnly,
        IReadOnlyList<string>? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId,
        string? clickUpFolderId,
        string? clickUpSpaceId,
        string? invoiceLabel,
        CancellationToken ct = default)
    {
        const string missingHoursSql = """
            lower(t.bill) = 'yes' and t.flat_fee is null and not (
                (t.billable_hours is not null or t.non_billable_hours is not null)
                and (coalesce(t.billable_hours, 0) > 0 or coalesce(t.non_billable_hours, 0) > 0)
            )
            """;
        const string completeStatusSql = """
            lower(trim(coalesce(t.clickup_status, ''))) = 'cancelled'
            and lower(trim(coalesce(t.bill, ''))) = 'no'
            """;
        const string missingSql = $"""
            not ({completeStatusSql})
            and (
                t.bill is null
                or ({missingHoursSql})
                or t.invoice_label is null or trim(t.invoice_label) = ''
            )
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
        ApplyTaskFilters(ref clientSql, clientParams, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            clickUpListId, clickUpFolderId, clickUpSpaceId, invoiceLabel, "t.");
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
        ApplyTaskFilters(ref monthSql, monthParams, clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            clickUpListId, clickUpFolderId, clickUpSpaceId, invoiceLabel, "t.");
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
        IReadOnlyList<string>? invoiced,
        Guid? projectId,
        bool? unassignedOnly,
        string? createdMonth,
        string? doneMonth,
        IReadOnlyList<string>? statuses,
        string? clickUpListId,
        string? clickUpFolderId,
        string? clickUpSpaceId,
        string? invoiceLabel,
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

        if (!string.IsNullOrWhiteSpace(clickUpListId))
        {
            sql += $" and {prefix}clickup_list_id = @clickUpListId";
            parameters.Add("clickUpListId", clickUpListId);
        }
        else if (!string.IsNullOrWhiteSpace(clickUpFolderId))
        {
            sql += $" and {prefix}clickup_folder_id = @clickUpFolderId";
            parameters.Add("clickUpFolderId", clickUpFolderId);
        }
        else if (!string.IsNullOrWhiteSpace(clickUpSpaceId))
        {
            sql += $"""
                 and {prefix}clickup_list_id in (
                    select l.external_id
                    from clickup_container l
                    where l.container_type = 'list'
                      and (
                        (l.parent_type = 'space' and l.parent_external_id = @clickUpSpaceId)
                        or (
                          l.parent_type = 'folder'
                          and l.parent_external_id in (
                            select f.external_id
                            from clickup_container f
                            where f.container_type = 'folder'
                              and f.parent_external_id = @clickUpSpaceId
                          )
                        )
                      )
                 )
                """;
            parameters.Add("clickUpSpaceId", clickUpSpaceId);
        }

        if (!string.IsNullOrWhiteSpace(invoiceLabel))
        {
            sql += $" and lower(trim({prefix}invoice_label)) = lower(trim(@invoiceLabel))";
            parameters.Add("invoiceLabel", invoiceLabel);
        }

        if (missingOnly == true)
        {
            sql += $"""
                 and not (
                    lower(trim(coalesce({prefix}clickup_status, ''))) = 'cancelled'
                    and lower(trim(coalesce({prefix}bill, ''))) = 'no'
                 )
                 and (
                    {prefix}bill is null
                    or (lower({prefix}bill) = 'yes' and {prefix}flat_fee is null and not (
                        ({prefix}billable_hours is not null or {prefix}non_billable_hours is not null)
                        and (coalesce({prefix}billable_hours, 0) > 0 or coalesce({prefix}non_billable_hours, 0) > 0)
                    ))
                    or {prefix}invoice_label is null or trim({prefix}invoice_label) = ''
                 )
                """;
        }

        ApplyInvoicedFilters(ref sql, invoiced, prefix);
    }

    private static void ApplyInvoicedFilters(ref string sql, IReadOnlyList<string>? invoiced, string prefix)
    {
        var selected = (invoiced ?? [])
            .Select(v => v.Trim().ToLowerInvariant())
            .Where(v => v is "paid" or "pending" or "none" or "no")
            .Select(v => v == "no" ? "none" : v)
            .Distinct()
            .ToList();

        // Empty or all three buckets → no filter (same as "all").
        if (selected.Count is 0 or 3)
            return;

        var parts = new List<string>();
        if (selected.Contains("paid"))
        {
            parts.Add($"""
                exists (
                   select 1 from invoice i
                   where lower(trim(i.name)) = lower(trim({prefix}invoice_label))
                     and lower(trim(i.status)) in ('fully-paid', 'partially-paid')
                )
                """);
        }

        if (selected.Contains("pending"))
        {
            parts.Add($"""
                exists (
                   select 1 from invoice i
                   where lower(trim(i.name)) = lower(trim({prefix}invoice_label))
                     and lower(trim(i.status)) in ('preparing', 'sent')
                )
                """);
        }

        if (selected.Contains("none"))
        {
            parts.Add($"""
                (
                   {prefix}invoice_label is null
                   or trim({prefix}invoice_label) = ''
                   or lower(trim({prefix}invoice_label)) = 'none'
                )
                """);
        }

        if (parts.Count == 0)
            return;

        sql += " and (" + string.Join(" or ", parts) + ")";
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
                (id, client_id, project_id, bill, billable_hours, non_billable_hours, invoice_label, discount_percent, flat_fee, note,
                 clickup_url, clickup_task_id, clickup_parent_id, clickup_folder_id, clickup_folder_name,
                 clickup_list_id, clickup_list_name, title, description, clickup_status, clickup_status_order, tags,
                 date_created, due_date, date_done, date_closed, order_index, estimated_hours, actual_hours,
                 created_at, updated_at)
            values
                ({t.Id}, {t.ClientId}, {t.ProjectId}, {t.Bill}, {t.BillableHours}, {t.NonBillableHours},
                 {t.InvoiceLabel}, {t.DiscountPercent}, {t.FlatFee}, {t.Note}, {t.ClickUpUrl}, {t.ClickUpTaskId}, {t.ClickUpParentId},
                 {t.ClickUpFolderId}, {t.ClickUpFolderName}, {t.ClickUpListId}, {t.ClickUpListName},
                 {t.Title}, {t.Description}, {t.ClickUpStatus}, {t.ClickUpStatusOrder}, {t.Tags}, {t.DateCreated}, {t.DueDate},
                 {t.DateDone}, {t.DateClosed}, {t.OrderIndex}, {t.EstimatedHours}, {t.ActualHours},
                 {t.CreatedAt}, {t.UpdatedAt})
            returning short_id
            """);
        using var conn = await factory.OpenAsync(ct);
        t.ShortId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return t.Id;
    }

    public async Task UpdateAsync(WorkTask t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update task set
                client_id = {t.ClientId}, project_id = {t.ProjectId},
                bill = {t.Bill}, billable_hours = {t.BillableHours}, non_billable_hours = {t.NonBillableHours},
                invoice_label = {t.InvoiceLabel}, discount_percent = {t.DiscountPercent}, flat_fee = {t.FlatFee}, note = {t.Note},
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
                bill = {t.Bill},
                billable_hours = {t.BillableHours},
                non_billable_hours = {t.NonBillableHours},
                invoice_label = {t.InvoiceLabel},
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

    public async Task<int> AssignProjectToUnassignedDescendantsAsync(
        string parentClickUpTaskId,
        Guid projectId,
        DateTimeOffset updatedAt,
        string? defaultInvoiceLabelForBillable = null,
        CancellationToken ct = default)
    {
        // Split paths so a null invoice label is not bound as an untyped Postgres parameter.
        var builder = string.IsNullOrWhiteSpace(defaultInvoiceLabelForBillable)
            ? SimpleBuilder.Create($"""
                with recursive descendants as (
                    select id, clickup_task_id
                    from task
                    where clickup_parent_id = {parentClickUpTaskId}
                      and clickup_task_id is not null
                    union all
                    select t.id, t.clickup_task_id
                    from task t
                    inner join descendants d on t.clickup_parent_id = d.clickup_task_id
                    where t.clickup_task_id is not null
                )
                update task set
                    project_id = {projectId},
                    updated_at = {updatedAt}
                where id in (select id from descendants)
                  and project_id is null
                """)
            : SimpleBuilder.Create($"""
                with recursive descendants as (
                    select id, clickup_task_id
                    from task
                    where clickup_parent_id = {parentClickUpTaskId}
                      and clickup_task_id is not null
                    union all
                    select t.id, t.clickup_task_id
                    from task t
                    inner join descendants d on t.clickup_parent_id = d.clickup_task_id
                    where t.clickup_task_id is not null
                )
                update task set
                    project_id = {projectId},
                    invoice_label = case
                        when lower(trim(coalesce(bill, ''))) = 'yes'
                             and (invoice_label is null or trim(invoice_label) = '')
                        then {defaultInvoiceLabelForBillable}
                        else invoice_label
                    end,
                    updated_at = {updatedAt}
                where id in (select id from descendants)
                  and project_id is null
                """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> FillEmptyHoursFromActualAsync(DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update task set
                billable_hours = case
                    when lower(trim(bill)) = 'yes'
                         and billable_hours is null
                         and actual_hours is not null
                    then actual_hours
                    else billable_hours
                end,
                non_billable_hours = case
                    when lower(trim(bill)) = 'no' and non_billable_hours is null
                    then coalesce(actual_hours, 0)
                    else non_billable_hours
                end,
                updated_at = {updatedAt}
            where bill is not null and trim(bill) <> ''
              and (
                (lower(trim(bill)) = 'yes' and billable_hours is null and actual_hours is not null)
                or (lower(trim(bill)) = 'no' and non_billable_hours is null)
              )
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> SetNoneInvoiceForNonBillableAsync(DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var none = InvoiceLabels.None;
        var builder = SimpleBuilder.Create($"""
            update task set
                invoice_label = {none},
                updated_at = {updatedAt}
            where lower(trim(coalesce(bill, ''))) = 'no'
              and (
                invoice_label is null
                or trim(invoice_label) = ''
              )
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByClickUpListIdAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<(string ListId, int Count)>(
            new CommandDefinition(
                """
                select clickup_list_id as ListId, count(*)::int as Count
                from task
                where clickup_list_id is not null and trim(clickup_list_id) <> ''
                group by clickup_list_id
                """,
                cancellationToken: ct));
        return rows.ToDictionary(r => r.ListId, r => r.Count);
    }

    public async Task<IReadOnlyList<string>> ListMissingParentClickUpIdsAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            """
            select distinct t.clickup_parent_id
            from task t
            where t.clickup_parent_id is not null
              and trim(t.clickup_parent_id) <> ''
              and not exists (
                  select 1 from task p where p.clickup_task_id = t.clickup_parent_id
              )
            order by t.clickup_parent_id
            """,
            cancellationToken: ct));
        return rows.ToList();
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

public sealed class ClickUpSyncRunRepository(IDbConnectionFactory factory) : IClickUpSyncRunRepository
{
    public async Task InsertAsync(ClickUpSyncRun run, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into clickup_sync_run
                (id, agency_id, started_at, finished_at, status, summary, log,
                 containers_upserted, tasks_created, tasks_updated, clients_created, parents_fetched)
            values
                ({run.Id}, {run.AgencyId}, {run.StartedAt}, {run.FinishedAt}, {run.Status}, {run.Summary}, {run.Log},
                 {run.ContainersUpserted}, {run.TasksCreated}, {run.TasksUpdated}, {run.ClientsCreated}, {run.ParentsFetched})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpdateAsync(ClickUpSyncRun run, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update clickup_sync_run set
                finished_at = {run.FinishedAt},
                status = {run.Status},
                summary = {run.Summary},
                log = {run.Log},
                containers_upserted = {run.ContainersUpserted},
                tasks_created = {run.TasksCreated},
                tasks_updated = {run.TasksUpdated},
                clients_created = {run.ClientsCreated},
                parents_fetched = {run.ParentsFetched}
            where id = {run.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<ClickUpSyncRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from clickup_sync_run where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ClickUpSyncRun>(
            new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ClickUpSyncRun>> ListRecentAsync(
        Guid agencyId, int limit = 20, CancellationToken ct = default)
    {
        var capped = Math.Clamp(limit, 1, 100);
        var builder = SimpleBuilder.Create($"""
            select * from clickup_sync_run
            where agency_id = {agencyId}
            order by started_at desc
            limit {capped}
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ClickUpSyncRun>(
            new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }
}
