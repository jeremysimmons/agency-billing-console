using Aib.Application.Abstractions;
using Aib.Domain;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class ExternalConnectionRepository(IDbConnectionFactory factory) : IExternalConnectionRepository
{
    public async Task<ExternalConnection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_connection where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalConnection>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<ExternalConnection?> GetByProviderWorkspaceAsync(string providerType, string? workspaceId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_connection where provider_type = {providerType} and external_workspace_id is not distinct from {workspaceId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalConnection>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExternalConnection>> ListAsync(Guid agencyId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_connection where agency_id = {agencyId} order by name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalConnection>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Guid> InsertAsync(ExternalConnection c, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_connection
                (id, agency_id, provider_type, name, external_workspace_id, authentication_reference,
                 status, last_successful_sync_at, last_attempted_sync_at, created_at, updated_at)
            values
                ({c.Id}, {c.AgencyId}, {c.ProviderType}, {c.Name}, {c.ExternalWorkspaceId}, {c.AuthenticationReference},
                 {c.Status}, {c.LastSuccessfulSyncAt}, {c.LastAttemptedSyncAt}, {c.CreatedAt}, {c.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return c.Id;
    }

    public async Task UpdateSyncAsync(Guid id, ExternalConnectionStatus status, DateTimeOffset attemptedAt, DateTimeOffset? successAt, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            update external_connection
            set status = {status}, last_attempted_sync_at = {attemptedAt},
                last_successful_sync_at = coalesce({successAt}, last_successful_sync_at), updated_at = {attemptedAt}
            where id = {id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class ExternalIdentityRepository(IDbConnectionFactory factory) : IExternalIdentityRepository
{
    public async Task<UpsertResult> UpsertAsync(ExternalIdentity i, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_identity
                (id, external_connection_id, user_id, contractor_id, external_user_id, external_username,
                 external_email, active, last_synced_at, created_at, updated_at)
            values
                ({i.Id}, {i.ExternalConnectionId}, {i.UserId}, {i.ContractorId}, {i.ExternalUserId}, {i.ExternalUsername},
                 {i.ExternalEmail}, {i.Active}, {i.LastSyncedAt}, {i.CreatedAt}, {i.UpdatedAt})
            on conflict (external_connection_id, external_user_id) do update set
                external_username = excluded.external_username,
                external_email = excluded.external_email,
                active = excluded.active,
                last_synced_at = excluded.last_synced_at,
                updated_at = excluded.updated_at
            returning id, (xmax = 0) as inserted
            """);
        return await UpsertExtensions.ExecUpsert(factory, b, ct);
    }
}

public sealed class ExternalContainerRepository(IDbConnectionFactory factory) : IExternalContainerRepository
{
    public async Task<Guid?> GetIdByExternalAsync(Guid connectionId, ContainerType type, string externalId, CancellationToken ct = default)
    {
        // Enums persist as their numeric value (Dapper sends the underlying int); match that representation.
        var b = SimpleBuilder.Create($"select id from external_container where external_connection_id = {connectionId} and container_type = {((int)type).ToString()} and external_id = {externalId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<UpsertResult> UpsertAsync(ExternalContainer c, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_container
                (id, external_connection_id, external_parent_id, external_id, container_type, name,
                 archived, raw_data_json, first_seen_at, last_seen_at, created_at, updated_at)
            values
                ({c.Id}, {c.ExternalConnectionId}, {c.ExternalParentId}, {c.ExternalId}, {c.ContainerType}, {c.Name},
                 {c.Archived}, {c.RawDataJson}::jsonb, {c.FirstSeenAt}, {c.LastSeenAt}, {c.CreatedAt}, {c.UpdatedAt})
            on conflict (external_connection_id, container_type, external_id) do update set
                external_parent_id = excluded.external_parent_id,
                name = excluded.name,
                archived = excluded.archived,
                raw_data_json = excluded.raw_data_json,
                last_seen_at = excluded.last_seen_at,
                updated_at = excluded.updated_at
            returning id, (xmax = 0) as inserted
            """);
        return await UpsertExtensions.ExecUpsert(factory, b, ct);
    }
}

public sealed class ExternalWorkItemRepository(IDbConnectionFactory factory) : IExternalWorkItemRepository
{
    public async Task<Guid?> GetIdByExternalAsync(Guid connectionId, string externalId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select id from external_work_item where external_connection_id = {connectionId} and external_id = {externalId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<UpsertResult> UpsertAsync(ExternalWorkItem w, CancellationToken ct = default)
    {
        // Idempotency: if the source row is unchanged, skip the write and report Unchanged.
        var probe = SimpleBuilder.Create($"select id, source_updated_at from external_work_item where external_connection_id = {w.ExternalConnectionId} and external_id = {w.ExternalId}");
        using var conn = await factory.OpenAsync(ct);
        var existing = await conn.QuerySingleOrDefaultAsync<WorkItemProbe>(new CommandDefinition(probe.Sql, probe.Parameters, cancellationToken: ct));
        if (existing is not null)
        {
            if (w.SourceUpdatedAt is { } incoming && existing.SourceUpdatedAt is { } prev
                && prev.ToUniversalTime() == incoming.UtcDateTime)
            {
                var touch = SimpleBuilder.Create($"update external_work_item set last_seen_at = {w.LastSeenAt}, last_synced_at = {w.LastSyncedAt} where id = {existing.Id}");
                await conn.ExecuteAsync(new CommandDefinition(touch.Sql, touch.Parameters, cancellationToken: ct));
                return new UpsertResult(existing.Id, ImportAction.Unchanged);
            }
        }

        var b = SimpleBuilder.Create($"""
            insert into external_work_item
                (id, external_connection_id, external_container_id, external_parent_work_item_id, external_id,
                 item_type, name, description, status_name, status_type, is_closed, archived,
                 assignee_external_user_id, start_date, due_date, completed_at, time_estimate_minutes,
                 time_spent_minutes, url, source_created_at, source_updated_at, raw_data_json,
                 first_seen_at, last_seen_at, last_synced_at)
            values
                ({w.Id}, {w.ExternalConnectionId}, {w.ExternalContainerId}, {w.ExternalParentWorkItemId}, {w.ExternalId},
                 {w.ItemType}, {w.Name}, {w.Description}, {w.StatusName}, {w.StatusType}, {w.IsClosed}, {w.Archived},
                 {w.AssigneeExternalUserId}, {w.StartDate}, {w.DueDate}, {w.CompletedAt}, {w.TimeEstimateMinutes},
                 {w.TimeSpentMinutes}, {w.Url}, {w.SourceCreatedAt}, {w.SourceUpdatedAt}, {w.RawDataJson}::jsonb,
                 {w.FirstSeenAt}, {w.LastSeenAt}, {w.LastSyncedAt})
            on conflict (external_connection_id, external_id) do update set
                external_container_id = excluded.external_container_id,
                external_parent_work_item_id = excluded.external_parent_work_item_id,
                item_type = excluded.item_type,
                name = excluded.name,
                description = excluded.description,
                status_name = excluded.status_name,
                status_type = excluded.status_type,
                is_closed = excluded.is_closed,
                archived = excluded.archived,
                assignee_external_user_id = excluded.assignee_external_user_id,
                start_date = excluded.start_date,
                due_date = excluded.due_date,
                completed_at = excluded.completed_at,
                time_estimate_minutes = excluded.time_estimate_minutes,
                time_spent_minutes = excluded.time_spent_minutes,
                url = excluded.url,
                source_created_at = excluded.source_created_at,
                source_updated_at = excluded.source_updated_at,
                raw_data_json = excluded.raw_data_json,
                last_seen_at = excluded.last_seen_at,
                last_synced_at = excluded.last_synced_at
            returning id, (xmax = 0) as inserted
            """);
        var row = await conn.QuerySingleAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return new UpsertResult((Guid)row.id, (bool)row.inserted ? ImportAction.Created : ImportAction.Updated);
    }
}

public sealed class ExternalTimeEntryRepository(IDbConnectionFactory factory) : IExternalTimeEntryRepository
{
    public async Task<UpsertResult> UpsertAsync(ExternalTimeEntry e, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_time_entry
                (id, external_connection_id, external_work_item_id, external_work_item_external_id, external_user_id,
                 external_id, work_date, started_at, ended_at, duration_minutes, description, billable,
                 source_created_at, source_updated_at, raw_data_json, last_synced_at)
            values
                ({e.Id}, {e.ExternalConnectionId}, {e.ExternalWorkItemId}, {e.ExternalWorkItemExternalId}, {e.ExternalUserId},
                 {e.ExternalId}, {e.WorkDate}, {e.StartedAt}, {e.EndedAt}, {e.DurationMinutes}, {e.Description}, {e.Billable},
                 {e.SourceCreatedAt}, {e.SourceUpdatedAt}, {e.RawDataJson}::jsonb, {e.LastSyncedAt})
            on conflict (external_connection_id, external_id) do update set
                external_work_item_id = excluded.external_work_item_id,
                external_work_item_external_id = excluded.external_work_item_external_id,
                external_user_id = excluded.external_user_id,
                work_date = excluded.work_date,
                started_at = excluded.started_at,
                ended_at = excluded.ended_at,
                duration_minutes = excluded.duration_minutes,
                description = excluded.description,
                billable = excluded.billable,
                source_created_at = excluded.source_created_at,
                source_updated_at = excluded.source_updated_at,
                raw_data_json = excluded.raw_data_json,
                last_synced_at = excluded.last_synced_at
            returning id, (xmax = 0) as inserted
            """);
        return await UpsertExtensions.ExecUpsert(factory, b, ct);
    }
}

public sealed class ImportRunRepository(IDbConnectionFactory factory) : IImportRunRepository
{
    public async Task<Guid> InsertAsync(ImportRun r, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into import_run
                (id, external_connection_id, import_type, status, started_at, completed_at, source_updated_after,
                 records_fetched, records_created, records_updated, records_unchanged, records_failed,
                 error_summary, triggered_by_user_id)
            values
                ({r.Id}, {r.ExternalConnectionId}, {r.ImportType}, {r.Status}, {r.StartedAt}, {r.CompletedAt}, {r.SourceUpdatedAfter},
                 {r.RecordsFetched}, {r.RecordsCreated}, {r.RecordsUpdated}, {r.RecordsUnchanged}, {r.RecordsFailed},
                 {r.ErrorSummary}, {r.TriggeredByUserId})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return r.Id;
    }

    public async Task UpdateAsync(ImportRun r, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            update import_run set
                status = {r.Status}, completed_at = {r.CompletedAt}, source_updated_after = {r.SourceUpdatedAfter},
                records_fetched = {r.RecordsFetched}, records_created = {r.RecordsCreated},
                records_updated = {r.RecordsUpdated}, records_unchanged = {r.RecordsUnchanged},
                records_failed = {r.RecordsFailed}, error_summary = {r.ErrorSummary}
            where id = {r.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<ImportRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from import_run where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ImportRun>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ImportRun>> ListByConnectionAsync(Guid connectionId, int limit, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from import_run where external_connection_id = {connectionId} order by started_at desc limit {limit}");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ImportRun>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class ImportRecordRepository(IDbConnectionFactory factory) : IImportRecordRepository
{
    public async Task InsertManyAsync(IEnumerable<ImportRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        const string sql = """
            insert into import_record
                (id, import_run_id, external_entity_type, external_entity_id, action, status,
                 external_record_id, error_message, imported_at)
            values
                (@Id, @ImportRunId, @ExternalEntityType, @ExternalEntityId, @Action, @Status,
                 @ExternalRecordId, @ErrorMessage, @ImportedAt)
            """;
        var rows = list.Select(r => new
        {
            r.Id, r.ImportRunId,
            ExternalEntityType = r.ExternalEntityType.ToString(),
            r.ExternalEntityId,
            Action = r.Action.ToString(),
            Status = r.Status.ToString(),
            r.ExternalRecordId, r.ErrorMessage, r.ImportedAt
        });
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, rows, cancellationToken: ct));
    }
}

public sealed class SyncCursorRepository(IDbConnectionFactory factory) : ISyncCursorRepository
{
    public async Task<SyncCursor?> GetAsync(Guid connectionId, ExternalEntityType entityType, CancellationToken ct = default)
    {
        // Enums persist as their numeric value (Dapper sends the underlying int); match that representation.
        var b = SimpleBuilder.Create($"select * from sync_cursor where external_connection_id = {connectionId} and entity_type = {((int)entityType).ToString()}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SyncCursor>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task UpsertAsync(SyncCursor c, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into sync_cursor
                (id, external_connection_id, entity_type, cursor_value, last_source_updated_at, last_successful_sync_at)
            values
                ({c.Id}, {c.ExternalConnectionId}, {c.EntityType}, {c.CursorValue}, {c.LastSourceUpdatedAt}, {c.LastSuccessfulSyncAt})
            on conflict (external_connection_id, entity_type) do update set
                cursor_value = excluded.cursor_value,
                last_source_updated_at = excluded.last_source_updated_at,
                last_successful_sync_at = excluded.last_successful_sync_at
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

file sealed record WorkItemProbe(Guid Id, DateTime? SourceUpdatedAt);

file static class UpsertExtensions
{
    public static async Task<UpsertResult> ExecUpsert(IDbConnectionFactory factory, Builder b, CancellationToken ct)
    {
        using var conn = await factory.OpenAsync(ct);
        var row = await conn.QuerySingleAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return new UpsertResult((Guid)row.id, (bool)row.inserted ? ImportAction.Created : ImportAction.Updated);
    }
}
