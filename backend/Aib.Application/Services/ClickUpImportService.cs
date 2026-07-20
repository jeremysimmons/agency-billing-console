using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Application.Integrations;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Application.Services;

/// <summary>
/// Orchestrates a ClickUp import: pages tasks + time entries into the external staging tables,
/// records diagnostics per entity, and advances sync cursors only after a successful run.
/// Upserts are idempotent (keyed by connection + external id).
/// </summary>
public sealed class ClickUpImportService(
    IClickUpClient clickUp,
    IExternalConnectionRepository connections,
    IExternalIdentityRepository identities,
    IExternalContainerRepository containers,
    IExternalWorkItemRepository workItems,
    IExternalTimeEntryRepository timeEntries,
    IImportRunRepository runs,
    IImportRecordRepository records,
    ISyncCursorRepository cursors,
    IAgencyRepository agencies,
    AccessService access,
    IClock clock,
    IOptions<ClickUpOptions> options,
    ILogger<ClickUpImportService> logger)
{
    private readonly ClickUpOptions _options = options.Value;

    public async Task<IReadOnlyList<ExternalConnectionDto>> ListConnectionsAsync(CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var list = await connections.ListAsync(agency.Id, ct);
        return list.Select(MapConnection).ToList();
    }

    public async Task<IReadOnlyList<ImportRunDto>> ListImportsAsync(Guid? connectionId, int limit, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var list = await runs.ListByConnectionAsync(connection.Id, limit, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ExternalConnection> ResolveConnectionAsync(Guid? connectionId, CancellationToken ct)
    {
        var connection = connectionId is { } id
            ? await connections.GetByIdAsync(id, ct)
            : await connections.GetByProviderWorkspaceAsync("clickup", _options.TeamId, ct);
        return connection ?? throw new NotFoundException("ClickUp connection not found.");
    }

    public async Task<ImportRunDto> RunImportAsync(Guid? connectionId, bool full, Guid? triggeredByUserId, CancellationToken ct = default)
    {
        // Manual triggers are contractor-side only; the scheduler passes no user and bypasses this check.
        if (triggeredByUserId is not null)
            access.EnsureCanManage();

        if (!_options.IsConfigured)
            throw new DomainException("ClickUp is not configured (missing API token or team id).");

        var connection = await ResolveConnectionAsync(connectionId, ct);
        var teamId = connection.ExternalWorkspaceId ?? _options.TeamId!;
        var now = clock.UtcNow;

        var itemCursor = await cursors.GetAsync(connection.Id, ExternalEntityType.WorkItem, ct);
        var timeCursor = await cursors.GetAsync(connection.Id, ExternalEntityType.TimeEntry, ct);

        DateTimeOffset? watermark = full ? null : itemCursor?.LastSourceUpdatedAt;
        long? dateUpdatedGtMs = watermark?.ToUnixTimeMilliseconds();

        var run = new ImportRun
        {
            Id = Guid.NewGuid(),
            ExternalConnectionId = connection.Id,
            ImportType = full ? ImportType.Full : (triggeredByUserId is null ? ImportType.Incremental : ImportType.Manual),
            Status = ImportStatus.Running,
            StartedAt = now,
            SourceUpdatedAfter = watermark,
            TriggeredByUserId = triggeredByUserId
        };
        await runs.InsertAsync(run, ct);

        var diagnostics = new List<ImportRecord>();
        var seenUsers = new HashSet<string>(StringComparer.Ordinal);
        DateTimeOffset? maxItemUpdated = watermark;

        try
        {
            // Task payloads usually omit space names — resolve once per run and refresh staging rows.
            var spaceNames = (await clickUp.GetSpacesAsync(teamId, ct))
                .ToDictionary(s => s.Id, s => s.Name, StringComparer.Ordinal);
            await RefreshSpaceContainersAsync(connection.Id, spaceNames, diagnostics, ct);

            // ---- Tasks + containers ----
            var page = 0;
            while (true)
            {
                var result = await clickUp.GetTasksAsync(teamId, dateUpdatedGtMs, _options.AssigneeId, page, ct);
                foreach (var task in result.Tasks)
                {
                    run.RecordsFetched++;
                    try
                    {
                        var containerId = await UpsertContainersAsync(connection.Id, task, spaceNames, diagnostics, ct);
                        await UpsertAssigneesAsync(connection.Id, task, seenUsers, diagnostics, ct);
                        var action = await UpsertWorkItemAsync(connection.Id, task, containerId, ct);
                        Tally(run, action);
                        diagnostics.Add(Diag(run.Id, ExternalEntityType.WorkItem, task.Id, action, ImportRecordStatus.Success, now));

                        if (task.SourceUpdatedAt is { } up && (maxItemUpdated is null || up > maxItemUpdated))
                            maxItemUpdated = up;
                    }
                    catch (Exception ex)
                    {
                        run.RecordsFailed++;
                        diagnostics.Add(Diag(run.Id, ExternalEntityType.WorkItem, task.Id, ImportAction.Failed, ImportRecordStatus.Failed, now, ex.Message));
                        logger.LogWarning(ex, "Failed to import ClickUp task {TaskId}", task.Id);
                    }
                }

                if (result.LastPage) break;
                page++;
            }

            // ---- Time entries ----
            var timeStartMs = (full ? null : timeCursor?.LastSourceUpdatedAt)?.ToUnixTimeMilliseconds()
                              ?? _options.InitialCreatedAfterMs;
            DateTimeOffset? maxTime = full ? null : timeCursor?.LastSourceUpdatedAt;

            var entries = await clickUp.GetTimeEntriesAsync(teamId, timeStartMs, ct);
            foreach (var entry in entries)
            {
                run.RecordsFetched++;
                try
                {
                    Guid? workItemId = entry.TaskId is { } tid
                        ? await workItems.GetIdByExternalAsync(connection.Id, tid, ct)
                        : null;

                    var e = new ExternalTimeEntry
                    {
                        Id = Guid.NewGuid(),
                        ExternalConnectionId = connection.Id,
                        ExternalWorkItemId = workItemId,
                        ExternalWorkItemExternalId = entry.TaskId,
                        ExternalUserId = entry.ExternalUserId,
                        ExternalId = entry.Id,
                        WorkDate = entry.WorkDate,
                        StartedAt = entry.StartedAt,
                        EndedAt = entry.EndedAt,
                        DurationMinutes = entry.DurationMinutes,
                        Description = entry.Description,
                        Billable = entry.Billable,
                        SourceCreatedAt = entry.SourceCreatedAt,
                        SourceUpdatedAt = entry.SourceUpdatedAt,
                        RawDataJson = entry.RawJson,
                        LastSyncedAt = now
                    };
                    var action = (await timeEntries.UpsertAsync(e, ct)).Action;
                    Tally(run, action);
                    diagnostics.Add(Diag(run.Id, ExternalEntityType.TimeEntry, entry.Id, action, ImportRecordStatus.Success, now));

                    var stamp = entry.SourceCreatedAt ?? entry.StartedAt;
                    if (stamp is { } st && (maxTime is null || st > maxTime)) maxTime = st;
                }
                catch (Exception ex)
                {
                    run.RecordsFailed++;
                    diagnostics.Add(Diag(run.Id, ExternalEntityType.TimeEntry, entry.Id, ImportAction.Failed, ImportRecordStatus.Failed, now, ex.Message));
                    logger.LogWarning(ex, "Failed to import ClickUp time entry {EntryId}", entry.Id);
                }
            }

            run.Status = run.RecordsFailed > 0 ? ImportStatus.CompletedWithErrors : ImportStatus.Completed;
            run.CompletedAt = clock.UtcNow;
            await runs.UpdateAsync(run, ct);
            foreach (var d in diagnostics) d.ImportRunId = run.Id;
            await records.InsertManyAsync(diagnostics, ct);

            // Advance cursors only after a successful completion.
            await cursors.UpsertAsync(new SyncCursor
            {
                Id = itemCursor?.Id ?? Guid.NewGuid(),
                ExternalConnectionId = connection.Id,
                EntityType = ExternalEntityType.WorkItem,
                LastSourceUpdatedAt = maxItemUpdated,
                LastSuccessfulSyncAt = run.CompletedAt
            }, ct);
            await cursors.UpsertAsync(new SyncCursor
            {
                Id = timeCursor?.Id ?? Guid.NewGuid(),
                ExternalConnectionId = connection.Id,
                EntityType = ExternalEntityType.TimeEntry,
                LastSourceUpdatedAt = maxTime,
                LastSuccessfulSyncAt = run.CompletedAt
            }, ct);

            await connections.UpdateSyncAsync(connection.Id, ExternalConnectionStatus.Active, now, run.CompletedAt, ct);
        }
        catch (Exception ex)
        {
            run.Status = ImportStatus.Failed;
            run.CompletedAt = clock.UtcNow;
            run.ErrorSummary = ex.Message;
            await runs.UpdateAsync(run, ct);
            foreach (var d in diagnostics) d.ImportRunId = run.Id;
            await records.InsertManyAsync(diagnostics, ct);
            await connections.UpdateSyncAsync(connection.Id, ExternalConnectionStatus.Error, now, null, ct);
            logger.LogError(ex, "ClickUp import failed for connection {ConnectionId}", connection.Id);
        }

        return Map(run);
    }

    private async Task RefreshSpaceContainersAsync(
        Guid connectionId, IReadOnlyDictionary<string, string> spaceNames,
        List<ImportRecord> diag, CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var (externalId, name) in spaceNames)
            await UpsertContainerAsync(connectionId, externalId, ContainerType.Space, name, null, "{}", now, diag, ct);
    }

    private async Task<Guid?> UpsertContainersAsync(
        Guid connectionId, ClickUpTask task, IReadOnlyDictionary<string, string> spaceNames,
        List<ImportRecord> diag, CancellationToken ct)
    {
        var now = clock.UtcNow;
        Guid? spaceId = null, folderId = null, listId = null;

        if (!string.IsNullOrEmpty(task.SpaceId))
        {
            var spaceName = task.SpaceName
                ?? (spaceNames.TryGetValue(task.SpaceId, out var n) ? n : null)
                ?? $"Space {task.SpaceId}";
            spaceId = await UpsertContainerAsync(connectionId, task.SpaceId!, ContainerType.Space, spaceName, null, task.RawJson, now, diag, ct);
        }

        if (!string.IsNullOrEmpty(task.FolderId) && !task.FolderHidden)
            folderId = await UpsertContainerAsync(connectionId, task.FolderId!, ContainerType.Folder, task.FolderName ?? "Folder", task.SpaceId, task.RawJson, now, diag, ct);

        if (!string.IsNullOrEmpty(task.ListId))
            listId = await UpsertContainerAsync(connectionId, task.ListId!, ContainerType.List, task.ListName ?? "List", task.FolderId ?? task.SpaceId, task.RawJson, now, diag, ct);

        return listId ?? folderId ?? spaceId;
    }

    private async Task<Guid> UpsertContainerAsync(
        Guid connectionId, string externalId, ContainerType type, string name, string? parentExternalId,
        string rawJson, DateTimeOffset now, List<ImportRecord> diag, CancellationToken ct)
    {
        var container = new ExternalContainer
        {
            Id = Guid.NewGuid(),
            ExternalConnectionId = connectionId,
            ExternalParentId = parentExternalId,
            ExternalId = externalId,
            ContainerType = type,
            Name = name,
            Archived = false,
            RawDataJson = null,
            FirstSeenAt = now,
            LastSeenAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        var result = await containers.UpsertAsync(container, ct);
        diag.Add(Diag(Guid.Empty, ExternalEntityType.Container, externalId, result.Action, ImportRecordStatus.Success, now));
        return result.Id;
    }

    private async Task UpsertAssigneesAsync(Guid connectionId, ClickUpTask task, HashSet<string> seen, List<ImportRecord> diag, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var users = new List<ClickUpUser>(task.Assignees);
        if (task.Creator is { } c) users.Add(c);

        foreach (var u in users)
        {
            if (string.IsNullOrEmpty(u.Id) || !seen.Add(u.Id)) continue;
            var result = await identities.UpsertAsync(new ExternalIdentity
            {
                Id = Guid.NewGuid(),
                ExternalConnectionId = connectionId,
                ExternalUserId = u.Id,
                ExternalUsername = u.Username,
                ExternalEmail = u.Email,
                Active = true,
                LastSyncedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
            diag.Add(Diag(Guid.Empty, ExternalEntityType.Identity, u.Id, result.Action, ImportRecordStatus.Success, now));
        }
    }

    private async Task<ImportAction> UpsertWorkItemAsync(Guid connectionId, ClickUpTask task, Guid? containerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var item = new ExternalWorkItem
        {
            Id = Guid.NewGuid(),
            ExternalConnectionId = connectionId,
            ExternalContainerId = containerId,
            ExternalParentWorkItemId = task.ParentId,
            ExternalId = task.Id,
            ItemType = string.IsNullOrEmpty(task.ParentId) ? WorkItemType.Task : WorkItemType.Subtask,
            Name = task.Name ?? "(untitled)",
            Description = task.Description,
            StatusName = task.StatusName,
            StatusType = task.StatusType,
            IsClosed = task.IsClosed,
            Archived = task.Archived,
            AssigneeExternalUserId = task.AssigneeExternalUserId,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            TimeEstimateMinutes = task.TimeEstimateMinutes,
            TimeSpentMinutes = task.TimeSpentMinutes,
            Url = task.Url,
            SourceCreatedAt = task.SourceCreatedAt,
            SourceUpdatedAt = task.SourceUpdatedAt,
            RawDataJson = task.RawJson,
            FirstSeenAt = now,
            LastSeenAt = now,
            LastSyncedAt = now
        };
        var result = await workItems.UpsertAsync(item, ct);
        return result.Action;
    }

    private static void Tally(ImportRun run, ImportAction action)
    {
        switch (action)
        {
            case ImportAction.Created: run.RecordsCreated++; break;
            case ImportAction.Updated: run.RecordsUpdated++; break;
            case ImportAction.Unchanged: run.RecordsUnchanged++; break;
            case ImportAction.Failed: run.RecordsFailed++; break;
        }
    }

    private static ImportRecord Diag(Guid runId, ExternalEntityType type, string externalId, ImportAction action,
        ImportRecordStatus status, DateTimeOffset now, string? error = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ImportRunId = runId,
            ExternalEntityType = type,
            ExternalEntityId = externalId,
            Action = action,
            Status = status,
            ErrorMessage = error,
            ImportedAt = now
        };

    private static ImportRunDto Map(ImportRun r) =>
        new(r.Id, r.ExternalConnectionId, r.ImportType, r.Status, r.StartedAt, r.CompletedAt, r.SourceUpdatedAfter,
            r.RecordsFetched, r.RecordsCreated, r.RecordsUpdated, r.RecordsUnchanged, r.RecordsFailed, r.ErrorSummary);

    private static ExternalConnectionDto MapConnection(ExternalConnection c) =>
        new(c.Id, c.ProviderType, c.Name, c.ExternalWorkspaceId, c.Status, c.LastSuccessfulSyncAt, c.LastAttemptedSyncAt);
}
