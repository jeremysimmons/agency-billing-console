using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Aib.Application.Services;

/// <summary>
/// Resolves ClickUp containers/work items onto internal clients/projects/tasks.
/// Never silently overwrites a Confirmed mapping; conflicts become Conflict status.
/// </summary>
public sealed class MappingService(
    IExternalConnectionRepository connections,
    IExternalContainerMappingRepository containerMappings,
    IExternalTaskMappingRepository taskMappings,
    IExternalStatusMappingRepository statusMappings,
    IMappingQueryRepository queries,
    IClientRepository clients,
    IProjectRepository projects,
    ITaskRepository tasks,
    IAgencyRepository agencies,
    AccessService access,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<MappingService> logger)
{
    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public async Task<ExternalConnection> ResolveConnectionAsync(Guid? connectionId, CancellationToken ct)
    {
        var c = connectionId is { } id
            ? await connections.GetByIdAsync(id, ct)
            : (await connections.ListAsync(
                (await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.")).Id, ct))
              .FirstOrDefault(x => x.ProviderType == "clickup");
        return c ?? throw new NotFoundException("ClickUp connection not found.");
    }

    // ---- Review lists ----

    public async Task<IReadOnlyList<UnmappedContainerDto>> ListUnmappedContainersAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var containers = await queries.ListContainersAsync(connection.Id, ct);
        var mappings = (await containerMappings.ListByConnectionAsync(connection.Id, null, ct))
            .ToDictionary(m => m.ExternalContainerId);
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var clientById = (await queries.ListClientsByAgencyAsync(agency.Id, ct)).ToDictionary(c => c.Id);
        var projectById = (await queries.ListProjectsByAgencyAsync(agency.Id, ct)).ToDictionary(p => p.Id);

        var result = new List<UnmappedContainerDto>();
        foreach (var c in containers.Where(x => x.ContainerType is ContainerType.Folder or ContainerType.List or ContainerType.Space))
        {
            mappings.TryGetValue(c.Id, out var m);
            if (m?.MappingStatus is MappingStatus.Confirmed or MappingStatus.Ignored) continue;

            result.Add(new UnmappedContainerDto(
                c.Id, c.ExternalId, c.ContainerType, c.Name, c.ExternalParentId,
                m?.Id, m?.MappingStatus,
                m?.ClientId, m?.ClientId is { } cid && clientById.TryGetValue(cid, out var cl) ? cl.Name : null,
                m?.ProjectId, m?.ProjectId is { } pid && projectById.TryGetValue(pid, out var pr) ? pr.Name : null));
        }
        return result;
    }

    public async Task<IReadOnlyList<UnmappedWorkItemDto>> ListUnmappedWorkItemsAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var items = await queries.ListWorkItemsAsync(connection.Id, ct);
        var mappings = (await taskMappings.ListByConnectionAsync(connection.Id, null, ct))
            .ToDictionary(m => m.ExternalWorkItemId);
        var containers = (await queries.ListContainersAsync(connection.Id, ct)).ToDictionary(c => c.Id);
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var tasksById = (await queries.ListTasksByAgencyAsync(agency.Id, ct)).ToDictionary(t => t.Id);

        var result = new List<UnmappedWorkItemDto>();
        foreach (var w in items)
        {
            mappings.TryGetValue(w.Id, out var m);
            if (m?.MappingStatus is MappingStatus.Confirmed or MappingStatus.Ignored) continue;

            containers.TryGetValue(w.ExternalContainerId ?? Guid.Empty, out var container);
            result.Add(new UnmappedWorkItemDto(
                w.Id, w.ExternalId, w.Name, w.StatusName, w.Url, w.ExternalParentWorkItemId,
                w.ExternalContainerId, container?.Name,
                m?.Id, m?.MappingStatus,
                m?.TaskId, m?.TaskId is { } tid && tasksById.TryGetValue(tid, out var t) ? t.Title : null,
                null, null));
        }
        return result;
    }

    public async Task<IReadOnlyList<ContainerMappingDto>> ListContainerMappingsAsync(Guid? connectionId, MappingStatus? status, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var mappings = await containerMappings.ListByConnectionAsync(connection.Id, status, ct);
        var containers = (await queries.ListContainersAsync(connection.Id, ct)).ToDictionary(c => c.Id);
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var clientById = (await queries.ListClientsByAgencyAsync(agency.Id, ct)).ToDictionary(c => c.Id);
        var projectById = (await queries.ListProjectsByAgencyAsync(agency.Id, ct)).ToDictionary(p => p.Id);

        return mappings.Select(m =>
        {
            containers.TryGetValue(m.ExternalContainerId, out var c);
            return new ContainerMappingDto(
                m.Id, m.ExternalContainerId, c?.Name ?? "?", c?.ContainerType ?? ContainerType.List,
                m.ClientId, m.ClientId is { } cid && clientById.TryGetValue(cid, out var cl) ? cl.Name : null,
                m.ProjectId, m.ProjectId is { } pid && projectById.TryGetValue(pid, out var pr) ? pr.Name : null,
                m.MappingStatus, m.MappingSource, m.Notes, m.MappedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<StatusMappingDto>> ListStatusMappingsAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var list = await statusMappings.ListByConnectionAsync(connection.Id, ct);
        return list.Select(s => new StatusMappingDto(
            s.Id, s.ExternalStatusName, s.ExternalStatusType, s.InternalStatus,
            s.TreatedAsCompleted, s.TreatedAsBillable, s.Active)).ToList();
    }

    // ---- Suggest ----

    public async Task<SuggestMappingsResult> SuggestAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");

        var statusSeeded = await SeedDefaultStatusMappingsAsync(connection.Id, ct);
        var containerSuggestions = await SuggestContainersAsync(connection.Id, agency.Id, ct);
        var taskSuggestions = await SuggestTasksAsync(connection.Id, agency.Id, ct);

        logger.LogInformation("Mapping suggest: {Containers} containers, {Tasks} tasks, {Statuses} status seeds",
            containerSuggestions, taskSuggestions, statusSeeded);
        return new SuggestMappingsResult(containerSuggestions, taskSuggestions, statusSeeded);
    }

    private async Task<int> SuggestContainersAsync(Guid connectionId, Guid agencyId, CancellationToken ct)
    {
        var containers = await queries.ListContainersAsync(connectionId, ct);
        var clients = await queries.ListClientsByAgencyAsync(agencyId, ct);
        var projects = await queries.ListProjectsByAgencyAsync(agencyId, ct);
        var clientByName = clients.GroupBy(c => Norm(c.Name)).ToDictionary(g => g.Key, g => g.First());
        var projectByName = projects.GroupBy(p => Norm(p.Name)).ToDictionary(g => g.Key, g => g.ToList());

        var count = 0;
        foreach (var c in containers.Where(x => x.ContainerType is ContainerType.Folder or ContainerType.List))
        {
            var existing = await containerMappings.GetByContainerIdAsync(c.Id, ct);
            if (existing?.MappingStatus is MappingStatus.Confirmed or MappingStatus.Ignored)
                continue;

            Guid? clientId = null;
            Guid? projectId = null;
            MappingStatus status = MappingStatus.Unmapped;

            if (c.ContainerType == ContainerType.Folder && clientByName.TryGetValue(Norm(c.Name), out var client))
            {
                clientId = client.Id;
                status = MappingStatus.Suggested;
            }
            else if (c.ContainerType == ContainerType.List)
            {
                // Exact list→project name match; inherit client from project.
                if (projectByName.TryGetValue(Norm(c.Name), out var matches))
                {
                    if (matches.Count == 1)
                    {
                        projectId = matches[0].Id;
                        clientId = matches[0].ClientId;
                        status = MappingStatus.Suggested;
                    }
                    else
                        status = MappingStatus.Conflict;
                }
                else if (clientByName.TryGetValue(Norm(c.Name), out var listAsClient))
                {
                    // Standalone-list → client (no project).
                    clientId = listAsClient.Id;
                    status = MappingStatus.Suggested;
                }
            }

            if (status is MappingStatus.Unmapped && existing is null)
                continue; // don't spam unmapped rows with no hint

            if (existing is not null && existing.MappingStatus == MappingStatus.Confirmed)
                continue;

            await containerMappings.UpsertAsync(new ExternalContainerMapping
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                ExternalContainerId = c.Id,
                ClientId = clientId ?? existing?.ClientId,
                ProjectId = projectId ?? existing?.ProjectId,
                MappingStatus = status,
                MappingSource = status == MappingStatus.Conflict ? MappingSource.NameMatch : MappingSource.NameMatch,
                Notes = existing?.Notes
            }, ct);
            count++;
        }
        return count;
    }

    private async Task<int> SuggestTasksAsync(Guid connectionId, Guid agencyId, CancellationToken ct)
    {
        var items = await queries.ListWorkItemsAsync(connectionId, ct);
        var internalTasks = await queries.ListTasksByAgencyAsync(agencyId, ct);
        var tasksByTitle = internalTasks.GroupBy(t => Norm(t.Title)).ToDictionary(g => g.Key, g => g.ToList());
        var externalById = items.ToDictionary(w => w.ExternalId);
        var containerMaps = (await containerMappings.ListByConnectionAsync(connectionId, MappingStatus.Confirmed, ct))
            .Concat(await containerMappings.ListByConnectionAsync(connectionId, MappingStatus.Suggested, ct))
            .GroupBy(m => m.ExternalContainerId)
            .ToDictionary(g => g.Key, g => g.First());

        var count = 0;
        // Parents first so children can inherit via ParentMapping.
        foreach (var w in items.OrderBy(x => x.ExternalParentWorkItemId is null ? 0 : 1))
        {
            var existing = await taskMappings.GetByWorkItemIdAsync(w.Id, ct);
            if (existing?.MappingStatus is MappingStatus.Confirmed or MappingStatus.Ignored)
                continue;

            Guid? taskId = null;
            MappingStatus status = MappingStatus.Unmapped;
            MappingSource source = MappingSource.NameMatch;

            // 1) Parent mapping: if parent is confirmed/suggested to a task, leave child for create-under-parent later.
            if (!string.IsNullOrEmpty(w.ExternalParentWorkItemId)
                && externalById.TryGetValue(w.ExternalParentWorkItemId!, out var parent)
                && await taskMappings.GetByWorkItemIdAsync(parent.Id, ct) is { MappingStatus: MappingStatus.Confirmed or MappingStatus.Suggested } parentMap
                && parentMap.TaskId is not null)
            {
                // Child suggestion: same client/project context via parent; title match under that tree preferred.
                if (tasksByTitle.TryGetValue(Norm(w.Name), out var titled))
                {
                    var underParent = titled.Where(t => t.ParentTaskId == parentMap.TaskId).ToList();
                    if (underParent.Count == 1)
                    {
                        taskId = underParent[0].Id;
                        status = MappingStatus.Suggested;
                        source = MappingSource.ParentMapping;
                    }
                    else if (titled.Count == 1)
                    {
                        taskId = titled[0].Id;
                        status = MappingStatus.Suggested;
                        source = MappingSource.NameMatch;
                    }
                    else if (titled.Count > 1)
                        status = MappingStatus.Conflict;
                }
            }
            else if (tasksByTitle.TryGetValue(Norm(w.Name), out var matches))
            {
                if (matches.Count == 1)
                {
                    taskId = matches[0].Id;
                    status = MappingStatus.Suggested;
                }
                else
                    status = MappingStatus.Conflict;
            }

            // Attach container-derived client/project hints via notes when no task match.
            if (status == MappingStatus.Unmapped && existing is null)
            {
                if (w.ExternalContainerId is { } cid && containerMaps.ContainsKey(cid))
                {
                    // Still create a Suggested row so review UI shows container-resolved candidates for create.
                    status = MappingStatus.Suggested;
                    source = MappingSource.Rule;
                }
                else continue;
            }

            await taskMappings.UpsertAsync(new ExternalTaskMapping
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                ExternalWorkItemId = w.Id,
                TaskId = taskId ?? existing?.TaskId,
                MappingStatus = status,
                MappingSource = source,
                Notes = existing?.Notes
            }, ct);
            count++;
        }
        return count;
    }

    public async Task<int> SeedDefaultStatusMappingsAsync(Guid connectionId, CancellationToken ct)
    {
        // Vocabulary from the sheet + common ClickUp defaults.
        var defaults = new (string Name, WorkStatus Internal, bool Completed, bool Billable)[]
        {
            ("to do", WorkStatus.Pending, false, true),
            ("open", WorkStatus.Pending, false, true),
            ("planning", WorkStatus.Pending, false, true),
            ("in progress", WorkStatus.InProgress, false, true),
            ("client review", WorkStatus.InProgress, false, true),
            ("internal review", WorkStatus.InProgress, false, true),
            ("update required", WorkStatus.InProgress, false, true),
            ("on hold", WorkStatus.Blocked, false, true),
            ("complete", WorkStatus.Completed, true, true),
            ("closed", WorkStatus.Completed, true, true),
            ("cancelled", WorkStatus.Cancelled, false, false),
            ("canceled", WorkStatus.Cancelled, false, false),
        };

        var existing = await statusMappings.ListByConnectionAsync(connectionId, ct);
        var have = existing.Select(s => Norm(s.ExternalStatusName)).ToHashSet();
        var seeded = 0;
        foreach (var (name, internalStatus, completed, billable) in defaults)
        {
            if (have.Contains(Norm(name))) continue;
            await statusMappings.UpsertAsync(new ExternalStatusMapping
            {
                Id = Guid.NewGuid(),
                ExternalConnectionId = connectionId,
                ExternalStatusName = name,
                InternalStatus = internalStatus,
                TreatedAsCompleted = completed,
                TreatedAsBillable = billable,
                Active = true
            }, ct);
            seeded++;
        }
        return seeded;
    }

    // ---- Confirm / ignore ----

    public async Task<ContainerMappingDto> ConfirmContainerAsync(Guid containerId, ConfirmContainerMappingRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(null, ct);
        var containers = (await queries.ListContainersAsync(connection.Id, ct)).ToDictionary(c => c.Id);
        if (!containers.TryGetValue(containerId, out var container))
            throw new NotFoundException("External container not found.");

        var mapping = await containerMappings.GetByContainerIdAsync(containerId, ct)
                      ?? new ExternalContainerMapping { Id = Guid.NewGuid(), ExternalContainerId = containerId };
        if (mapping.MappingStatus == MappingStatus.Confirmed)
            throw new DomainException("Confirmed mappings cannot be silently overwritten. Re-open as Suggested first.");

        var agency = await agencies.GetDefaultAsync(ct) ?? throw new NotFoundException("Agency not configured.");
        var now = clock.UtcNow;
        Guid? clientId = request.ClientId;
        Guid? projectId = request.ProjectId;

        if (request.CreateClient)
        {
            var client = new Client
            {
                Id = Guid.NewGuid(), AgencyId = agency.Id, Name = container.Name.Trim(),
                Status = ClientStatus.Active, Active = true, CreatedAt = now, UpdatedAt = now
            };
            await clients.InsertAsync(client, ct);
            clientId = client.Id;
        }

        if (request.CreateProject)
        {
            if (clientId is null)
                throw new DomainException("CreateProject requires a ClientId or CreateClient.");
            var project = new Project
            {
                Id = Guid.NewGuid(), ClientId = clientId.Value, Name = container.Name.Trim(),
                Status = ProjectStatus.Active, BillingType = BillingType.Hourly,
                Active = true, CreatedAt = now, UpdatedAt = now
            };
            await projects.InsertAsync(project, ct);
            projectId = project.Id;
        }

        if (clientId is null && projectId is null)
            throw new DomainException("Confirm requires a client and/or project.");

        if (projectId is { } pid)
        {
            var project = await projects.GetByIdAsync(pid, ct) ?? throw new NotFoundException("Project not found.");
            clientId ??= project.ClientId;
            if (clientId != project.ClientId)
                throw new DomainException("Project does not belong to the selected client.");
        }

        mapping.ClientId = clientId;
        mapping.ProjectId = projectId;
        mapping.MappingStatus = MappingStatus.Confirmed;
        mapping.MappingSource = request.CreateClient || request.CreateProject ? MappingSource.ImportCreated : MappingSource.Manual;
        mapping.MappedByUserId = currentUser.UserId;
        mapping.MappedAt = now;
        mapping.Notes = request.Notes ?? mapping.Notes;
        await containerMappings.UpsertAsync(mapping, ct);

        return (await ListContainerMappingsAsync(connection.Id, null, ct)).First(m => m.Id == mapping.Id);
    }

    public async Task<TaskMappingDto> ConfirmTaskAsync(Guid workItemId, ConfirmTaskMappingRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(null, ct);
        var items = (await queries.ListWorkItemsAsync(connection.Id, ct)).ToDictionary(w => w.Id);
        if (!items.TryGetValue(workItemId, out var workItem))
            throw new NotFoundException("External work item not found.");

        var mapping = await taskMappings.GetByWorkItemIdAsync(workItemId, ct)
                      ?? new ExternalTaskMapping { Id = Guid.NewGuid(), ExternalWorkItemId = workItemId };
        if (mapping.MappingStatus == MappingStatus.Confirmed)
            throw new DomainException("Confirmed mappings cannot be silently overwritten.");

        var now = clock.UtcNow;
        Guid? taskId = request.TaskId;

        if (request.CreateTask)
        {
            var (clientId, projectId, parentTaskId) = await ResolvePlacementAsync(connection.Id, workItem, ct);
            if (clientId is null)
                throw new DomainException("Cannot create task: map the list/folder to a client first.");

            var statusMap = workItem.StatusName is { } sn
                ? await statusMappings.GetByStatusNameAsync(connection.Id, sn, ct)
                : null;

            var task = new WorkTask
            {
                Id = Guid.NewGuid(),
                ClientId = clientId.Value,
                ProjectId = projectId,
                ParentTaskId = parentTaskId,
                Title = workItem.Name,
                Description = workItem.Description,
                WorkStatus = statusMap?.InternalStatus ?? WorkStatus.Pending,
                BillingStatus = statusMap is { TreatedAsCompleted: true }
                    ? BillingStatus.PendingReview
                    : BillingStatus.NotReady,
                BillingType = BillingType.Hourly,
                Billable = statusMap?.TreatedAsBillable ?? true,
                EstimatedMinutes = workItem.TimeEstimateMinutes,
                EstimateRollupMode = RollupMode.Direct,
                ActualRollupMode = RollupMode.DirectAndChildren,
                BillingRollupMode = BillingRollupMode.Task,
                DueDate = workItem.DueDate is { } d ? DateOnly.FromDateTime(d.UtcDateTime) : null,
                CompletedAt = statusMap is { TreatedAsCompleted: true } ? workItem.CompletedAt ?? now : null,
                CreatedAt = now,
                UpdatedAt = now
            };
            await tasks.InsertAsync(task, ct);
            taskId = task.Id;
        }

        if (taskId is null)
            throw new DomainException("Confirm requires TaskId or CreateTask.");

        _ = await tasks.GetByIdAsync(taskId.Value, ct) ?? throw new NotFoundException("Task not found.");

        mapping.TaskId = taskId;
        mapping.MappingStatus = MappingStatus.Confirmed;
        mapping.MappingSource = request.CreateTask ? MappingSource.ImportCreated : MappingSource.Manual;
        mapping.MappedByUserId = currentUser.UserId;
        mapping.MappedAt = now;
        mapping.Notes = request.Notes ?? mapping.Notes;
        await taskMappings.UpsertAsync(mapping, ct);

        return new TaskMappingDto(
            mapping.Id, mapping.ExternalWorkItemId, workItem.Name, workItem.Url,
            mapping.TaskId, workItem.Name, mapping.MappingStatus, mapping.MappingSource,
            mapping.Notes, mapping.MappedAt);
    }

    public async Task IgnoreContainerAsync(Guid containerId, IgnoreMappingRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var mapping = await containerMappings.GetByContainerIdAsync(containerId, ct)
                      ?? new ExternalContainerMapping { Id = Guid.NewGuid(), ExternalContainerId = containerId };
        mapping.MappingStatus = MappingStatus.Ignored;
        mapping.MappedByUserId = currentUser.UserId;
        mapping.MappedAt = clock.UtcNow;
        mapping.Notes = request.Notes ?? mapping.Notes;
        await containerMappings.UpsertAsync(mapping, ct);
    }

    public async Task IgnoreTaskAsync(Guid workItemId, IgnoreMappingRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var mapping = await taskMappings.GetByWorkItemIdAsync(workItemId, ct)
                      ?? new ExternalTaskMapping { Id = Guid.NewGuid(), ExternalWorkItemId = workItemId };
        mapping.MappingStatus = MappingStatus.Ignored;
        mapping.MappedByUserId = currentUser.UserId;
        mapping.MappedAt = clock.UtcNow;
        mapping.Notes = request.Notes ?? mapping.Notes;
        await taskMappings.UpsertAsync(mapping, ct);
    }

    public async Task<StatusMappingDto> UpsertStatusMappingAsync(Guid? connectionId, UpsertStatusMappingRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.ExternalStatusName))
            throw new DomainException("Status name is required.");
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var existing = await statusMappings.GetByStatusNameAsync(connection.Id, request.ExternalStatusName.Trim(), ct);
        var row = new ExternalStatusMapping
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ExternalConnectionId = connection.Id,
            ExternalStatusName = request.ExternalStatusName.Trim().ToLowerInvariant(),
            ExternalStatusType = request.ExternalStatusType,
            InternalStatus = request.InternalStatus,
            TreatedAsCompleted = request.TreatedAsCompleted,
            TreatedAsBillable = request.TreatedAsBillable,
            Active = request.Active
        };
        await statusMappings.UpsertAsync(row, ct);
        return new StatusMappingDto(row.Id, row.ExternalStatusName, row.ExternalStatusType, row.InternalStatus,
            row.TreatedAsCompleted, row.TreatedAsBillable, row.Active);
    }

    /// <summary>Push ClickUp statuses onto confirmed internal tasks using status mappings.</summary>
    public async Task<ApplyMappedStatusesResult> ApplyStatusesAsync(Guid? connectionId, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var connection = await ResolveConnectionAsync(connectionId, ct);
        var confirmed = await taskMappings.ListByConnectionAsync(connection.Id, MappingStatus.Confirmed, ct);
        var items = (await queries.ListWorkItemsAsync(connection.Id, ct)).ToDictionary(w => w.Id);
        var statusByName = (await statusMappings.ListByConnectionAsync(connection.Id, ct))
            .Where(s => s.Active)
            .ToDictionary(s => Norm(s.ExternalStatusName), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        var now = clock.UtcNow;
        foreach (var m in confirmed.Where(x => x.TaskId is not null))
        {
            if (!items.TryGetValue(m.ExternalWorkItemId, out var w) || w.StatusName is null) continue;
            if (!statusByName.TryGetValue(Norm(w.StatusName), out var sm)) continue;
            var task = await tasks.GetByIdAsync(m.TaskId!.Value, ct);
            if (task is null) continue;

            var changed = false;
            if (task.WorkStatus != sm.InternalStatus)
            {
                task.WorkStatus = sm.InternalStatus;
                changed = true;
            }
            if (sm.TreatedAsCompleted)
            {
                if (task.CompletedAt is null) { task.CompletedAt = w.CompletedAt ?? now; changed = true; }
                if (task.BillingStatus is BillingStatus.NotReady)
                {
                    task.BillingStatus = BillingStatus.PendingReview;
                    changed = true;
                }
            }
            else if (task.WorkStatus != WorkStatus.Completed && task.CompletedAt is not null)
            {
                task.CompletedAt = null;
                changed = true;
            }

            if (!changed) continue;
            task.UpdatedAt = now;
            await tasks.UpdateAsync(task, ct);
            updated++;
        }
        return new ApplyMappedStatusesResult(updated);
    }

    private async Task<(Guid? ClientId, Guid? ProjectId, Guid? ParentTaskId)> ResolvePlacementAsync(
        Guid connectionId, ExternalWorkItem workItem, CancellationToken ct)
    {
        Guid? parentTaskId = null;
        if (!string.IsNullOrEmpty(workItem.ExternalParentWorkItemId))
        {
            var items = await queries.ListWorkItemsAsync(connectionId, ct);
            var parent = items.FirstOrDefault(i => i.ExternalId == workItem.ExternalParentWorkItemId);
            if (parent is not null)
            {
                var parentMap = await taskMappings.GetByWorkItemIdAsync(parent.Id, ct);
                if (parentMap is { MappingStatus: MappingStatus.Confirmed, TaskId: { } pid })
                {
                    parentTaskId = pid;
                    var parentTask = await tasks.GetByIdAsync(pid, ct);
                    if (parentTask is not null)
                        return (parentTask.ClientId, parentTask.ProjectId, parentTaskId);
                }
            }
        }

        if (workItem.ExternalContainerId is { } containerId)
        {
            var cmap = await containerMappings.GetByContainerIdAsync(containerId, ct);
            if (cmap is { MappingStatus: MappingStatus.Confirmed or MappingStatus.Suggested })
                return (cmap.ClientId, cmap.ProjectId, parentTaskId);
        }

        return (null, null, parentTaskId);
    }
}
