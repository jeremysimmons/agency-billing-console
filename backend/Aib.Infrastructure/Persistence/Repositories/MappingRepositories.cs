using Aib.Application.Abstractions;
using Aib.Domain;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class ExternalContainerMappingRepository(IDbConnectionFactory factory) : IExternalContainerMappingRepository
{
    public async Task<ExternalContainerMapping?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_container_mapping where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalContainerMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<ExternalContainerMapping?> GetByContainerIdAsync(Guid containerId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_container_mapping where external_container_id = {containerId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalContainerMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExternalContainerMapping>> ListByConnectionAsync(Guid connectionId, MappingStatus? status, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select m.* from external_container_mapping m
            join external_container c on c.id = m.external_container_id
            where c.external_connection_id = {connectionId}
            """);
        if (status is { } s)
            b.Append($" and m.mapping_status = {((int)s).ToString()}");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalContainerMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task UpsertAsync(ExternalContainerMapping m, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_container_mapping
                (id, external_container_id, client_id, project_id, mapping_status, mapping_source,
                 mapped_by_user_id, mapped_at, notes)
            values
                ({m.Id}, {m.ExternalContainerId}, {m.ClientId}, {m.ProjectId}, {m.MappingStatus}, {m.MappingSource},
                 {m.MappedByUserId}, {m.MappedAt}, {m.Notes})
            on conflict (external_container_id) do update set
                client_id = excluded.client_id,
                project_id = excluded.project_id,
                mapping_status = excluded.mapping_status,
                mapping_source = excluded.mapping_source,
                mapped_by_user_id = excluded.mapped_by_user_id,
                mapped_at = excluded.mapped_at,
                notes = excluded.notes
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class ExternalTaskMappingRepository(IDbConnectionFactory factory) : IExternalTaskMappingRepository
{
    public async Task<ExternalTaskMapping?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_task_mapping where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalTaskMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<ExternalTaskMapping?> GetByWorkItemIdAsync(Guid workItemId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_task_mapping where external_work_item_id = {workItemId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalTaskMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExternalTaskMapping>> ListByConnectionAsync(Guid connectionId, MappingStatus? status, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select m.* from external_task_mapping m
            join external_work_item w on w.id = m.external_work_item_id
            where w.external_connection_id = {connectionId}
            """);
        if (status is { } s)
            b.Append($" and m.mapping_status = {((int)s).ToString()}");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalTaskMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task UpsertAsync(ExternalTaskMapping m, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_task_mapping
                (id, external_work_item_id, task_id, mapping_status, mapping_source,
                 mapped_by_user_id, mapped_at, notes)
            values
                ({m.Id}, {m.ExternalWorkItemId}, {m.TaskId}, {m.MappingStatus}, {m.MappingSource},
                 {m.MappedByUserId}, {m.MappedAt}, {m.Notes})
            on conflict (external_work_item_id) do update set
                task_id = excluded.task_id,
                mapping_status = excluded.mapping_status,
                mapping_source = excluded.mapping_source,
                mapped_by_user_id = excluded.mapped_by_user_id,
                mapped_at = excluded.mapped_at,
                notes = excluded.notes
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class ExternalStatusMappingRepository(IDbConnectionFactory factory) : IExternalStatusMappingRepository
{
    public async Task<IReadOnlyList<ExternalStatusMapping>> ListByConnectionAsync(Guid connectionId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_status_mapping where external_connection_id = {connectionId} order by external_status_name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalStatusMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ExternalStatusMapping?> GetByStatusNameAsync(Guid connectionId, string statusName, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_status_mapping where external_connection_id = {connectionId} and lower(external_status_name) = lower({statusName}) and active = true");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ExternalStatusMapping>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }

    public async Task UpsertAsync(ExternalStatusMapping m, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            insert into external_status_mapping
                (id, external_connection_id, external_status_name, external_status_type, internal_status,
                 treated_as_completed, treated_as_billable, active)
            values
                ({m.Id}, {m.ExternalConnectionId}, {m.ExternalStatusName}, {m.ExternalStatusType}, {m.InternalStatus},
                 {m.TreatedAsCompleted}, {m.TreatedAsBillable}, {m.Active})
            on conflict (external_connection_id, external_status_name) do update set
                external_status_type = excluded.external_status_type,
                internal_status = excluded.internal_status,
                treated_as_completed = excluded.treated_as_completed,
                treated_as_billable = excluded.treated_as_billable,
                active = excluded.active
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
    }
}

public sealed class MappingQueryRepository(IDbConnectionFactory factory) : IMappingQueryRepository
{
    public async Task<IReadOnlyList<ExternalContainer>> ListContainersAsync(Guid connectionId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_container where external_connection_id = {connectionId} order by container_type, name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalContainer>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ExternalWorkItem>> ListWorkItemsAsync(Guid connectionId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from external_work_item where external_connection_id = {connectionId} order by name");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ExternalWorkItem>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Client>> ListClientsByAgencyAsync(Guid agencyId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"select * from client where agency_id = {agencyId} and active = true");
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Client>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Project>> ListProjectsByAgencyAsync(Guid agencyId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select p.* from project p
            join client c on c.id = p.client_id
            where c.agency_id = {agencyId} and p.active = true
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Project>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WorkTask>> ListTasksByAgencyAsync(Guid agencyId, CancellationToken ct = default)
    {
        var b = SimpleBuilder.Create($"""
            select t.* from task t
            join client c on c.id = t.client_id
            where c.agency_id = {agencyId}
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<WorkTask>(new CommandDefinition(b.Sql, b.Parameters, cancellationToken: ct));
        return rows.ToList();
    }
}
