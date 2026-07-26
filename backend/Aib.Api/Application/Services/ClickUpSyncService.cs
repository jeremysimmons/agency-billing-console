using System.Globalization;
using System.Text;
using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Application.Integrations;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aib.Application.Services;

public sealed class ClickUpSyncService(
    IClickUpClient clickUp,
    IClickUpHierarchyBuilder hierarchyBuilder,
    IClickUpContainerRepository containers,
    IClientRepository clients,
    IProjectRepository projects,
    ITaskRepository tasks,
    IAgencyRepository agencies,
    IClickUpSyncRunRepository syncRuns,
    IClock clock,
    IOptions<ClickUpOptions> options,
    ILogger<ClickUpSyncService> logger)
{
    public async Task<ClickUpSyncResultDto> SyncAsync(
        Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress = null,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
            throw new DomainException("ClickUp is not configured (missing API token or team id).");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var now = clock.UtcNow;
        var teamId = opts.TeamId!;

        var run = new ClickUpSyncRun
        {
            Id = Guid.NewGuid(),
            AgencyId = agency.Id,
            StartedAt = now,
            Status = "running",
        };
        await syncRuns.InsertAsync(run, ct);

        var log = new SyncLogBuffer(run.Id, logger);
        await log.WriteAsync(
            reportProgress,
            $"Sync started (team={teamId}, assignee={opts.AssigneeId ?? "(none)"}, " +
            $"createdAfterMs={opts.InitialCreatedAfterMs}, pageLimit={opts.PageLimit})",
            ct);

        try
        {
            await ReportAsync(reportProgress, new ClickUpSyncProgressEvent(
                "started", Message: "Sync started", SyncRunId: run.Id), ct);

            var hierarchyRows = await hierarchyBuilder.BuildAsync(teamId, ct);
            var containerEntities = hierarchyRows.Select(r => new ClickUpContainer
            {
                Id = Guid.NewGuid(),
                ContainerType = r.Type,
                ExternalId = r.Id,
                Name = r.Name,
                ParentType = r.ParentType,
                ParentExternalId = r.ParentId,
                UpdatedAt = now
            }).ToList();
            await containers.UpsertManyAsync(containerEntities, ct);
            await log.WriteAsync(
                reportProgress,
                $"Hierarchy upserted: {containerEntities.Count} containers " +
                $"({hierarchyRows.Count(r => r.Type == ClickUpHierarchyTypes.Space)} spaces, " +
                $"{hierarchyRows.Count(r => r.Type == ClickUpHierarchyTypes.Folder)} folders, " +
                $"{hierarchyRows.Count(r => r.Type == ClickUpHierarchyTypes.List)} lists)",
                ct);

            await ReportAsync(
                reportProgress,
                new ClickUpSyncProgressEvent(
                    "hierarchy",
                    Message: "Hierarchy upserted",
                    ContainersUpserted: containerEntities.Count,
                    SyncRunId: run.Id),
                ct);

            var clientsCreated = 0;
            var tasksCreated = 0;
            var tasksUpdated = 0;
            var page = 0;
            var clientLocations = new Dictionary<Guid, ClientLocationHint>();
            var seenTaskIds = new HashSet<string>(StringComparer.Ordinal);

            while (true)
            {
                await log.WriteAsync(
                    reportProgress,
                    $"Fetching task page {page} (assignee filter={(string.IsNullOrWhiteSpace(opts.AssigneeId) ? "off" : opts.AssigneeId)})…",
                    ct);
                var result = await clickUp.GetTasksAsync(teamId, opts.AssigneeId, page, ct);
                await log.WriteAsync(
                    reportProgress,
                    $"Page {page}: pulled {result.Tasks.Count} task(s) from ClickUp (lastPage={result.LastPage})",
                    ct);

                foreach (var remote in result.Tasks)
                {
                    seenTaskIds.Add(remote.Id);
                    var client = await EnsureClientAsync(agency.Id, remote, now, ct);
                    if (client.WasCreated)
                    {
                        clientsCreated++;
                        await log.WriteAsync(
                            reportProgress,
                            $"  client CREATE {client.Client.Name} " +
                            $"(folder={client.Client.ClickUpFolderId ?? "-"}, list={client.Client.ClickUpListId ?? "-"})",
                            ct);
                    }

                    RememberClientLocation(clientLocations, client.Client.Id, remote);

                    var existing = !string.IsNullOrWhiteSpace(remote.Url)
                        ? await tasks.GetByClickUpUrlAsync(remote.Url, ct)
                        : await tasks.GetByClickUpTaskIdAsync(remote.Id, ct);

                    if (existing is null)
                    {
                        var task = MapNewTask(remote, client.Client.Id, now);
                        await tasks.InsertAsync(task, ct);
                        tasksCreated++;
                        await log.WriteAsync(
                            reportProgress,
                            $"  task CREATE {FormatTask(remote)}",
                            ct);
                    }
                    else
                    {
                        ApplyApiFields(existing, remote, client.Client.Id, now);
                        await tasks.UpdateApiFieldsAsync(existing, ct);
                        tasksUpdated++;
                        await log.WriteAsync(
                            reportProgress,
                            $"  task UPDATE {FormatTask(remote)}",
                            ct);
                    }
                }

                await ReportAsync(
                    reportProgress,
                    new ClickUpSyncProgressEvent(
                        "page",
                        Message: $"Processed page {page}",
                        Page: page,
                        ContainersUpserted: containerEntities.Count,
                        TasksCreated: tasksCreated,
                        TasksUpdated: tasksUpdated,
                        ClientsCreated: clientsCreated,
                        SyncRunId: run.Id),
                    ct);

                if (result.LastPage) break;
                page++;
            }

            await log.WriteAsync(
                reportProgress,
                $"Assignee pull complete: {seenTaskIds.Count} unique task(s), " +
                $"{tasksCreated} created, {tasksUpdated} updated, {clientsCreated} new clients",
                ct);

            var parentsFetched = await ResolveMissingParentsAsync(
                agency.Id, now, clientLocations, reportProgress, log, ct);
            tasksCreated += parentsFetched;

            await RefreshClientBillFieldsAsync(clientLocations, hierarchyRows, now, reportProgress, log, ct);

            await log.WriteAsync(reportProgress, "Filling empty hours from ClickUp tracked time…", ct);
            await ReportAsync(
                reportProgress,
                new ClickUpSyncProgressEvent("hours", Message: "Filling empty hours from ClickUp", SyncRunId: run.Id),
                ct);
            var hoursFilled = await tasks.FillEmptyHoursFromActualAsync(now, ct);
            await log.WriteAsync(reportProgress, $"Filled hours on {hoursFilled} task(s)", ct);

            await log.WriteAsync(reportProgress, "Setting invoice=none for non-billable tasks…", ct);
            await ReportAsync(
                reportProgress,
                new ClickUpSyncProgressEvent("invoices", Message: "Setting none invoice for non-billable", SyncRunId: run.Id),
                ct);
            var invoicesSet = await tasks.SetNoneInvoiceForNonBillableAsync(now, ct);
            await log.WriteAsync(reportProgress, $"Set none invoice on {invoicesSet} task(s)", ct);

            var stillMissing = await tasks.ListMissingParentClickUpIdsAsync(ct);
            if (stillMissing.Count > 0)
            {
                await log.WriteAsync(
                    reportProgress,
                    $"WARNING: {stillMissing.Count} parent id(s) still missing after fetch: {string.Join(", ", stillMissing)}",
                    ct);
            }
            else
            {
                await log.WriteAsync(reportProgress, "All referenced ClickUp parents are present locally", ct);
            }

            var summary = $"Synced {tasksCreated + tasksUpdated} tasks ({tasksCreated} new, {tasksUpdated} updated), " +
                          $"{containerEntities.Count} containers, {clientsCreated} new clients" +
                          (parentsFetched > 0 ? $", fetched {parentsFetched} missing parents" : "") +
                          (hoursFilled > 0 ? $", filled hours on {hoursFilled} tasks" : "") +
                          (invoicesSet > 0 ? $", set none invoice on {invoicesSet} tasks" : "") + ".";
            await agencies.UpdateSyncSummaryAsync(agency.Id, now, summary, ct);
            logger.LogInformation("{Summary}", summary);
            await log.WriteAsync(reportProgress, summary, ct);

            run.FinishedAt = clock.UtcNow;
            run.Status = "completed";
            run.Summary = summary;
            run.Log = log.Text;
            run.ContainersUpserted = containerEntities.Count;
            run.TasksCreated = tasksCreated;
            run.TasksUpdated = tasksUpdated;
            run.ClientsCreated = clientsCreated;
            run.ParentsFetched = parentsFetched;
            await syncRuns.UpdateAsync(run, ct);

            var dto = new ClickUpSyncResultDto(
                now, containerEntities.Count, tasksCreated, tasksUpdated, clientsCreated, summary, run.Id, parentsFetched);
            await ReportAsync(
                reportProgress,
                new ClickUpSyncProgressEvent(
                    "completed",
                    Message: summary,
                    ContainersUpserted: containerEntities.Count,
                    TasksCreated: tasksCreated,
                    TasksUpdated: tasksUpdated,
                    ClientsCreated: clientsCreated,
                    ParentsFetched: parentsFetched,
                    SyncedAt: now,
                    Summary: summary,
                    SyncRunId: run.Id),
                ct);

            return dto;
        }
        catch (Exception ex)
        {
            await log.WriteAsync(reportProgress, $"ERROR: {ex.Message}", CancellationToken.None);
            run.FinishedAt = clock.UtcNow;
            run.Status = "failed";
            run.Summary = ex.Message;
            run.Log = log.Text;
            try { await syncRuns.UpdateAsync(run, CancellationToken.None); }
            catch (Exception persistEx)
            {
                logger.LogError(persistEx, "Failed to persist sync run failure log {SyncRunId}", run.Id);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<ClickUpSyncRunSummaryDto>> ListSyncRunsAsync(
        int limit = 20, CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var rows = await syncRuns.ListRecentAsync(agency.Id, limit, ct);
        return rows.Select(r => new ClickUpSyncRunSummaryDto(
            r.Id, r.StartedAt, r.FinishedAt, r.Status, r.Summary,
            r.ContainersUpserted, r.TasksCreated, r.TasksUpdated, r.ClientsCreated, r.ParentsFetched)).ToList();
    }

    public async Task<ClickUpSyncRunDto> GetSyncRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await syncRuns.GetByIdAsync(id, ct)
                  ?? throw new NotFoundException("Sync run not found.");
        return new ClickUpSyncRunDto(
            run.Id, run.StartedAt, run.FinishedAt, run.Status, run.Summary, run.Log,
            run.ContainersUpserted, run.TasksCreated, run.TasksUpdated, run.ClientsCreated, run.ParentsFetched);
    }

    public async Task<TaskDto> SyncTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
            throw new DomainException("ClickUp is not configured (missing API token or team id).");

        var task = await tasks.GetByIdAsync(taskId, ct)
                   ?? throw new NotFoundException("Task not found.");
        if (string.IsNullOrWhiteSpace(task.ClickUpTaskId))
            throw new DomainException("Task has no ClickUp id to sync.");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var now = clock.UtcNow;
        var remote = await clickUp.GetTaskAsync(task.ClickUpTaskId, ct);
        var client = await EnsureClientAsync(agency.Id, remote, now, ct);
        ApplyApiFields(task, remote, client.Client.Id, now);
        await tasks.UpdateApiFieldsAsync(task, ct);

        string? projectName = null;
        if (task.ProjectId is { } pid)
            projectName = (await projects.GetByIdAsync(pid, ct))?.Name;

        return new TaskDto(
            task.Id, task.ShortId, task.ClientId, client.Client.Name, task.ProjectId, projectName,
            task.Bill, task.BillableHours, task.NonBillableHours, task.InvoiceLabel, task.DiscountPercent, task.Note,
            task.ClickUpUrl, task.ClickUpTaskId, task.ClickUpParentId,
            task.ClickUpFolderId, task.ClickUpFolderName, task.ClickUpListId, task.ClickUpListName,
            task.Title, task.Description, task.ClickUpStatus, task.Tags,
            task.DateCreated, task.DueDate, task.DateDone, task.DateClosed,
            task.OrderIndex, task.EstimatedHours, task.ActualHours,
            NeedsAttention(task));
    }

    private async Task<int> ResolveMissingParentsAsync(
        Guid agencyId,
        DateTimeOffset now,
        Dictionary<Guid, ClientLocationHint> clientLocations,
        Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress,
        SyncLogBuffer log,
        CancellationToken ct)
    {
        var queue = new Queue<string>(await tasks.ListMissingParentClickUpIdsAsync(ct));
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        var fetched = 0;

        if (queue.Count == 0)
        {
            await log.WriteAsync(reportProgress, "No missing parent tasks to fetch", ct);
            return 0;
        }

        await log.WriteAsync(
            reportProgress,
            $"Resolving {queue.Count} missing parent task(s) not returned by assignee filter…",
            ct);
        await ReportAsync(
            reportProgress,
            new ClickUpSyncProgressEvent(
                "parents",
                Message: "Fetching missing parent tasks",
                SyncRunId: log.SyncRunId),
            ct);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!attempted.Add(parentId))
                continue;

            if (await tasks.GetByClickUpTaskIdAsync(parentId, ct) is not null)
                continue;

            try
            {
                await log.WriteAsync(reportProgress, $"  parent FETCH {parentId}…", ct);
                var remote = await clickUp.GetTaskAsync(parentId, ct);
                var client = await EnsureClientAsync(agencyId, remote, now, ct);
                if (client.WasCreated)
                {
                    await log.WriteAsync(
                        reportProgress,
                        $"  client CREATE {client.Client.Name} (via parent {parentId})",
                        ct);
                }

                RememberClientLocation(clientLocations, client.Client.Id, remote);

                var existing = !string.IsNullOrWhiteSpace(remote.Url)
                    ? await tasks.GetByClickUpUrlAsync(remote.Url, ct)
                    : await tasks.GetByClickUpTaskIdAsync(remote.Id, ct);

                if (existing is null)
                {
                    var task = MapNewTask(remote, client.Client.Id, now);
                    await tasks.InsertAsync(task, ct);
                    fetched++;
                    await log.WriteAsync(
                        reportProgress,
                        $"  parent CREATE {FormatTask(remote)}",
                        ct);
                }
                else
                {
                    ApplyApiFields(existing, remote, client.Client.Id, now);
                    await tasks.UpdateApiFieldsAsync(existing, ct);
                    await log.WriteAsync(
                        reportProgress,
                        $"  parent UPDATE {FormatTask(remote)}",
                        ct);
                }

                if (!string.IsNullOrWhiteSpace(remote.ParentId)
                    && !attempted.Contains(remote.ParentId)
                    && await tasks.GetByClickUpTaskIdAsync(remote.ParentId, ct) is null)
                {
                    queue.Enqueue(remote.ParentId);
                    await log.WriteAsync(
                        reportProgress,
                        $"  parent queue {remote.ParentId} (ancestor of {remote.Id})",
                        ct);
                }
            }
            catch (Exception ex)
            {
                await log.WriteAsync(
                    reportProgress,
                    $"  parent FAIL {parentId}: {ex.Message}",
                    ct);
                logger.LogWarning(ex, "Failed to fetch missing ClickUp parent {ParentId}", parentId);
            }
        }

        await log.WriteAsync(reportProgress, $"Missing-parent resolve complete: {fetched} created", ct);
        return fetched;
    }

    private static string FormatTask(ClickUpTask remote)
    {
        var title = string.IsNullOrWhiteSpace(remote.Name) ? "(untitled)" : remote.Name.Trim();
        if (title.Length > 80) title = title[..77] + "...";
        var parent = string.IsNullOrWhiteSpace(remote.ParentId) ? "-" : remote.ParentId;
        var list = remote.ListName ?? remote.ListId ?? "-";
        var folder = remote.FolderName ?? remote.FolderId ?? "-";
        var status = remote.StatusName ?? "-";
        var hours = remote.ActualHours?.ToString(CultureInfo.InvariantCulture) ?? "-";
        return $"{remote.Id} \"{title}\" parent={parent} status={status} " +
               $"folder={folder} list={list} hours={hours} url={remote.Url ?? "-"}";
    }

    private static bool NeedsAttention(WorkTask t) =>
        !IsComplete(t)
        && (string.IsNullOrWhiteSpace(t.Bill)
            || (string.Equals(t.Bill, "yes", StringComparison.OrdinalIgnoreCase)
                && !((t.BillableHours is not null || t.NonBillableHours is not null)
                     && ((t.BillableHours ?? 0) > 0 || (t.NonBillableHours ?? 0) > 0)))
            || string.IsNullOrWhiteSpace(t.InvoiceLabel));

    private static bool IsComplete(WorkTask t) =>
        string.Equals(t.ClickUpStatus?.Trim(), "cancelled", StringComparison.OrdinalIgnoreCase)
        && string.Equals(t.Bill?.Trim(), "no", StringComparison.OrdinalIgnoreCase);

    private static async Task ReportAsync(
        Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress,
        ClickUpSyncProgressEvent evt,
        CancellationToken ct)
    {
        if (reportProgress is not null)
            await reportProgress(evt, ct);
    }

    public async Task<IReadOnlyList<ClickUpHierarchyNodeDto>> GetHierarchyAsync(CancellationToken ct = default)
    {
        var rows = await containers.ListAllAsync(ct);
        var taskCountsByList = await tasks.CountByClickUpListIdAsync(ct);
        var byParent = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ParentExternalId))
            .GroupBy(r => r.ParentExternalId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

        var workspaces = rows
            .Where(r => r.ContainerType == ClickUpHierarchyTypes.Workspace)
            .OrderBy(r => r.Name)
            .Select(r => BuildNode(r, byParent, taskCountsByList))
            .ToList();
        if (workspaces.Count > 0)
            return workspaces;

        // Legacy data: no workspace row — wrap spaces under configured team id.
        var spaces = rows
            .Where(r => r.ContainerType == ClickUpHierarchyTypes.Space)
            .OrderBy(r => r.Name)
            .Select(r => BuildNode(r, byParent, taskCountsByList))
            .ToList();
        var teamId = options.Value.TeamId;
        if (string.IsNullOrWhiteSpace(teamId) || spaces.Count == 0)
            return spaces;

        return
        [
            new ClickUpHierarchyNodeDto(
                ClickUpHierarchyTypes.Workspace,
                teamId,
                "Workspace",
                null,
                null,
                clock.UtcNow,
                spaces.Sum(s => s.TaskCount),
                spaces)
        ];
    }

    private static ClickUpHierarchyNodeDto BuildNode(
        ClickUpContainer node,
        IReadOnlyDictionary<string, List<ClickUpContainer>> byParent,
        IReadOnlyDictionary<string, int> taskCountsByList)
    {
        var children = byParent.TryGetValue(node.ExternalId, out var kids)
            ? kids.Select(k => BuildNode(k, byParent, taskCountsByList)).ToList()
            : [];
        var ownCount = string.Equals(node.ContainerType, ClickUpHierarchyTypes.List, StringComparison.OrdinalIgnoreCase)
            ? taskCountsByList.GetValueOrDefault(node.ExternalId)
            : 0;
        var taskCount = ownCount + children.Sum(c => c.TaskCount);
        return new ClickUpHierarchyNodeDto(
            node.ContainerType,
            node.ExternalId,
            node.Name,
            node.ParentType,
            node.ParentExternalId,
            node.UpdatedAt,
            taskCount,
            children);
    }

    private async Task<(Client Client, bool WasCreated)> EnsureClientAsync(
        Guid agencyId, ClickUpTask remote, DateTimeOffset now, CancellationToken ct)
    {
        var folderId = remote.FolderId;
        var listId = remote.ListId;
        var displayName = remote.FolderName ?? remote.ListName ?? "Unknown Client";
        var clientIsFolder = !string.IsNullOrWhiteSpace(folderId) && !remote.FolderHidden;
        var parsed = ClickUpFolderNaming.Parse(displayName);
        var name = parsed.Name.Length > 0 ? parsed.Name : displayName;

        // Client location is either a ClickUp folder or a ClickUp list (space-level /
        // hidden folder) — never both. Match only on that location's id.
        Client? existing;
        string? keyFolderId;
        string? keyListId;
        if (clientIsFolder)
        {
            keyFolderId = folderId;
            keyListId = null;
            existing = await clients.GetByClickUpFolderIdAsync(folderId!, ct)
                       // Legacy: same id may have been stored in the list column.
                       ?? await clients.GetByClickUpListIdAsync(folderId!, ct);
        }
        else if (!string.IsNullOrWhiteSpace(listId))
        {
            keyFolderId = null;
            keyListId = listId;
            existing = await clients.GetByClickUpListIdAsync(listId!, ct)
                       ?? await clients.GetByClickUpFolderIdAsync(listId!, ct);
        }
        else
        {
            throw new DomainException(
                $"ClickUp task {remote.Id} has no folder or list id; cannot resolve client.");
        }

        if (existing is not null)
        {
            await AttachClickUpKeysAndNameAsync(existing, displayName, keyFolderId, keyListId, now, ct);
            return (existing, false);
        }

        var client = new Client
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            Name = name,
            Code = parsed.Code,
            OriginalName = parsed.OriginalName,
            ClickUpFolderId = keyFolderId,
            ClickUpListId = keyListId,
            Status = ClientStatus.Active,
            Active = true,
            BillFieldAvailable = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return (client, true);
    }

    private async Task AttachClickUpKeysAndNameAsync(
        Client existing,
        string displayName,
        string? folderId,
        string? listId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var (_, _, original) = ClickUpFolderNaming.Parse(displayName);
        var changed = false;

        // Name is user-editable and must not be overwritten on sync.
        // Keep OriginalName in sync with the ClickUp folder/list title.
        if (existing.OriginalName != original)
        {
            existing.OriginalName = original;
            changed = true;
        }

        // Keep exactly one ClickUp location key (folder XOR list).
        if (existing.ClickUpFolderId != folderId)
        {
            existing.ClickUpFolderId = folderId;
            changed = true;
        }

        if (existing.ClickUpListId != listId)
        {
            existing.ClickUpListId = listId;
            changed = true;
        }

        if (!changed) return;
        existing.UpdatedAt = now;
        await clients.UpdateAsync(existing, ct);
    }

    private static void RememberClientLocation(
        Dictionary<Guid, ClientLocationHint> hints,
        Guid clientId,
        ClickUpTask remote)
    {
        if (!hints.TryGetValue(clientId, out var hint))
        {
            hints[clientId] = new ClientLocationHint(remote.ListId, remote.FolderId, remote.FolderHidden);
            return;
        }

        hints[clientId] = hint with
        {
            ListId = hint.ListId ?? remote.ListId,
            FolderId = hint.FolderId ?? remote.FolderId,
            FolderHidden = hint.FolderHidden || remote.FolderHidden,
        };
    }

    private async Task RefreshClientBillFieldsAsync(
        IReadOnlyDictionary<Guid, ClientLocationHint> locations,
        IReadOnlyList<ClickUpHierarchyNode> hierarchy,
        DateTimeOffset now,
        Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress,
        SyncLogBuffer log,
        CancellationToken ct)
    {
        var opts = options.Value;
        var spaceByFolder = hierarchy
            .Where(r => r.Type == ClickUpHierarchyTypes.Folder
                        && r.ParentType == ClickUpHierarchyTypes.Space
                        && !string.IsNullOrWhiteSpace(r.ParentId))
            .ToDictionary(r => r.Id, r => r.ParentId!, StringComparer.Ordinal);
        var spaceByList = hierarchy
            .Where(r => r.Type == ClickUpHierarchyTypes.List)
            .ToDictionary(r => r.Id, r =>
            {
                if (r.ParentType == ClickUpHierarchyTypes.Space)
                    return r.ParentId;
                if (r.ParentType == ClickUpHierarchyTypes.Folder
                    && r.ParentId is { } folderId
                    && spaceByFolder.TryGetValue(folderId, out var spaceId))
                    return spaceId;
                return null;
            }, StringComparer.Ordinal);

        var clientsTotal = locations.Count;
        var clientsProcessed = 0;
        await log.WriteAsync(
            reportProgress,
            $"Probing billable custom fields for {clientsTotal} client location(s) (parallel)…",
            ct);
        await log.ReportProgressAsync(
            reportProgress,
            new ClickUpSyncProgressEvent(
                "bill_fields",
                Message: "Probing billable custom fields",
                ClientsProcessed: 0,
                ClientsTotal: clientsTotal,
                SyncRunId: log.SyncRunId),
            ct);

        await Parallel.ForEachAsync(
            locations,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (pair, token) =>
            {
                var (clientId, hint) = pair;
                var client = await clients.GetByIdAsync(clientId, token);
                if (client is null) return;

                try
                {
                    var fields = await LoadAccessibleFieldsAsync(hint, spaceByList, spaceByFolder, token);
                    var billable = FindBillableField(fields, opts);
                    if (billable is null)
                    {
                        client.BillFieldAvailable = false;
                        client.BillCustomFieldId = null;
                        client.BillYesOptionId = null;
                        client.BillNoOptionId = null;
                        await log.WriteAsync(
                            reportProgress,
                            $"  bill-field MISS {client.Name} (list={hint.ListId ?? "-"}, folder={hint.FolderId ?? "-"})",
                            token);
                    }
                    else
                    {
                        client.BillFieldAvailable = true;
                        client.BillCustomFieldId = billable.Id;
                        client.BillYesOptionId = FindOptionId(billable, opts.BillYesOptionId, "yes", "y", "true")
                            ?? opts.BillYesOptionId;
                        client.BillNoOptionId = FindOptionId(billable, opts.BillNoOptionId, "no", "n", "false")
                            ?? opts.BillNoOptionId;
                        await log.WriteAsync(
                            reportProgress,
                            $"  bill-field OK {client.Name} field={billable.Id} \"{billable.Name}\"",
                            token);
                    }

                    client.BillFieldCheckedAt = now;
                    client.UpdatedAt = now;
                    await clients.UpdateAsync(client, token);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to probe billable custom fields for client {ClientId}", clientId);
                    await log.WriteAsync(
                        reportProgress,
                        $"  bill-field FAIL {client.Name}: {ex.Message}",
                        token);
                    client.BillFieldAvailable = false;
                    client.BillFieldCheckedAt = now;
                    client.UpdatedAt = now;
                    await clients.UpdateAsync(client, token);
                }

                var done = Interlocked.Increment(ref clientsProcessed);
                await log.ReportProgressAsync(
                    reportProgress,
                    new ClickUpSyncProgressEvent(
                        "bill_fields",
                        Message: "Probing billable custom fields",
                        ClientsProcessed: done,
                        ClientsTotal: clientsTotal,
                        SyncRunId: log.SyncRunId),
                    token);
            });
    }

    private async Task<IReadOnlyList<ClickUpCustomField>> LoadAccessibleFieldsAsync(
        ClientLocationHint hint,
        IReadOnlyDictionary<string, string?> spaceByList,
        IReadOnlyDictionary<string, string> spaceByFolder,
        CancellationToken ct)
    {
        var merged = new Dictionary<string, ClickUpCustomField>(StringComparer.Ordinal);

        async Task MergeFrom(Func<CancellationToken, Task<IReadOnlyList<ClickUpCustomField>>> loader)
        {
            try
            {
                foreach (var field in await loader(ct))
                    merged.TryAdd(field.Id, field);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Custom field probe failed for one ClickUp location");
            }
        }

        if (!string.IsNullOrWhiteSpace(hint.ListId))
            await MergeFrom(c => clickUp.GetListCustomFieldsAsync(hint.ListId!, c));

        if (!string.IsNullOrWhiteSpace(hint.FolderId) && !hint.FolderHidden)
            await MergeFrom(c => clickUp.GetFolderCustomFieldsAsync(hint.FolderId!, c));

        string? spaceId = null;
        if (!string.IsNullOrWhiteSpace(hint.ListId) && spaceByList.TryGetValue(hint.ListId!, out var fromList))
            spaceId = fromList;
        if (spaceId is null
            && !string.IsNullOrWhiteSpace(hint.FolderId)
            && spaceByFolder.TryGetValue(hint.FolderId!, out var fromFolder))
            spaceId = fromFolder;

        if (!string.IsNullOrWhiteSpace(spaceId))
            await MergeFrom(c => clickUp.GetSpaceCustomFieldsAsync(spaceId, c));

        return merged.Values.ToList();
    }

    private static ClickUpCustomField? FindBillableField(
        IReadOnlyList<ClickUpCustomField> fields,
        ClickUpOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.BillCustomFieldId))
        {
            var byId = fields.FirstOrDefault(f =>
                string.Equals(f.Id, opts.BillCustomFieldId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return byId;
        }

        var fieldName = string.IsNullOrWhiteSpace(opts.BillFieldName) ? "Billable" : opts.BillFieldName;
        return fields.FirstOrDefault(f =>
            string.Equals(f.Type, "drop_down", StringComparison.OrdinalIgnoreCase)
            && string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindOptionId(
        ClickUpCustomField field,
        string? preferredId,
        params string[] names)
    {
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var byId = field.Options.FirstOrDefault(o =>
                string.Equals(o.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return byId.Id;
        }

        foreach (var name in names)
        {
            var match = field.Options.FirstOrDefault(o =>
                string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Id;
        }

        return null;
    }

    private WorkTask MapNewTask(ClickUpTask remote, Guid clientId, DateTimeOffset now) =>
        ApplyApiFields(new WorkTask
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            CreatedAt = now,
            UpdatedAt = now
        }, remote, clientId, now);

    private WorkTask ApplyApiFields(WorkTask task, ClickUpTask remote, Guid clientId, DateTimeOffset now)
    {
        task.ClientId = clientId;
        task.ClickUpUrl = remote.Url;
        task.ClickUpTaskId = remote.Id;
        task.ClickUpParentId = remote.ParentId;
        task.ClickUpFolderId = remote.FolderId;
        task.ClickUpFolderName = remote.FolderName;
        task.ClickUpListId = remote.ListId;
        task.ClickUpListName = remote.ListName;
        task.Title = remote.Name ?? "(untitled)";
        task.Description = remote.Description;
        task.ClickUpStatus = remote.StatusName;
        task.ClickUpStatusOrder = remote.StatusOrderIndex;
        task.Tags = remote.Tags.Count > 0 ? string.Join(';', remote.Tags) : null;
        task.DateCreated = remote.SourceCreatedAt;
        task.DueDate = remote.DueDate;
        task.DateDone = remote.CompletedAt;
        task.DateClosed = remote.ClosedAt;
        task.OrderIndex = remote.OrderIndex;
        task.EstimatedHours = remote.EstimatedHours;
        task.ActualHours = remote.ActualHours;
        if (TryResolveBill(remote, out var bill))
            task.Bill = bill;
        ApplyClickUpHoursForBill(task);
        ApplyInvoiceForBill(task);
        task.UpdatedAt = now;
        return task;
    }

    /// <summary>
    /// Same as UI bill change: fill empty billable/non-billable hours from ClickUp tracked hours.
    /// Bill=no with no ClickUp hours → non-billable 0.
    /// </summary>
    private static void ApplyClickUpHoursForBill(WorkTask task)
    {
        var billNorm = task.Bill?.Trim();
        if (string.Equals(billNorm, "yes", StringComparison.OrdinalIgnoreCase)
            && task.BillableHours is null
            && task.ActualHours is not null)
        {
            task.BillableHours = task.ActualHours;
            return;
        }

        if (string.Equals(billNorm, "no", StringComparison.OrdinalIgnoreCase)
            && task.NonBillableHours is null)
        {
            task.NonBillableHours = task.ActualHours ?? 0;
        }
    }

    private static void ApplyInvoiceForBill(WorkTask task)
    {
        if (string.Equals(task.Bill?.Trim(), "no", StringComparison.OrdinalIgnoreCase))
            task.InvoiceLabel = InvoiceLabels.None;
    }

    /// <summary>
    /// Maps ClickUp Billable dropdown → local task.bill ("yes"/"no"/null).
    /// Returns false when the field is absent so local bill is left unchanged.
    /// </summary>
    private bool TryResolveBill(ClickUpTask remote, out string? bill)
    {
        bill = null;
        var opts = options.Value;
        var fieldName = string.IsNullOrWhiteSpace(opts.BillFieldName) ? "Billable" : opts.BillFieldName;
        var field = remote.CustomFields.FirstOrDefault(f =>
            (!string.IsNullOrWhiteSpace(opts.BillCustomFieldId)
             && string.Equals(f.Id, opts.BillCustomFieldId, StringComparison.OrdinalIgnoreCase))
            || string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        if (field is null)
            return false;

        if (string.IsNullOrWhiteSpace(field.Value))
        {
            bill = null;
            return true;
        }

        var option = ResolveDropdownOption(field);
        if (option is null)
            return false;

        if (MatchesBillOption(option, opts.BillYesOptionId, "yes", "y", "true"))
        {
            bill = "yes";
            return true;
        }

        if (MatchesBillOption(option, opts.BillNoOptionId, "no", "n", "false"))
        {
            bill = "no";
            return true;
        }

        return false;
    }

    private static ClickUpCustomFieldOption? ResolveDropdownOption(ClickUpTaskCustomField field)
    {
        if (string.IsNullOrWhiteSpace(field.Value))
            return null;

        // Task API returns dropdown value as option orderindex; write API uses option id.
        if (int.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderIndex))
        {
            var byOrder = field.Options.FirstOrDefault(o => o.OrderIndex == orderIndex);
            if (byOrder is not null)
                return byOrder;
        }

        return field.Options.FirstOrDefault(o =>
            string.Equals(o.Id, field.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesBillOption(
        ClickUpCustomFieldOption option,
        string? configuredOptionId,
        params string[] names)
    {
        if (!string.IsNullOrWhiteSpace(configuredOptionId)
            && string.Equals(option.Id, configuredOptionId, StringComparison.OrdinalIgnoreCase))
            return true;

        return names.Any(n => string.Equals(option.Name, n, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ClientLocationHint(string? ListId, string? FolderId, bool FolderHidden);

    private sealed class SyncLogBuffer(Guid syncRunId, ILogger logger)
    {
        private readonly StringBuilder _sb = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public Guid SyncRunId { get; } = syncRunId;
        public string Text => _sb.ToString();

        public async Task WriteAsync(
            Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress,
            string message,
            CancellationToken ct)
        {
            var line = $"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}Z] {message}";
            await _gate.WaitAsync(ct);
            try
            {
                _sb.AppendLine(line);
                logger.LogInformation("ClickUp sync {SyncRunId}: {Message}", SyncRunId, message);
                if (reportProgress is not null)
                {
                    await reportProgress(
                        new ClickUpSyncProgressEvent("log", Message: line, SyncRunId: SyncRunId),
                        ct);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ReportProgressAsync(
            Func<ClickUpSyncProgressEvent, CancellationToken, Task>? reportProgress,
            ClickUpSyncProgressEvent evt,
            CancellationToken ct)
        {
            if (reportProgress is null) return;
            await _gate.WaitAsync(ct);
            try
            {
                await reportProgress(evt, ct);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
