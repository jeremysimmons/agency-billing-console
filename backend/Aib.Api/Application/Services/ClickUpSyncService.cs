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
    ITaskRepository tasks,
    IAgencyRepository agencies,
    IClock clock,
    IOptions<ClickUpOptions> options,
    ILogger<ClickUpSyncService> logger)
{
    public async Task<ClickUpSyncResultDto> SyncAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
            throw new DomainException("ClickUp is not configured (missing API token or team id).");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var now = clock.UtcNow;
        var teamId = opts.TeamId!;

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

        var clientsCreated = 0;
        var tasksCreated = 0;
        var tasksUpdated = 0;
        var page = 0;
        var clientLocations = new Dictionary<Guid, ClientLocationHint>();

        while (true)
        {
            var result = await clickUp.GetTasksAsync(teamId, opts.AssigneeId, page, ct);
            foreach (var remote in result.Tasks)
            {
                var client = await EnsureClientAsync(agency.Id, remote, now, ct);
                if (client.WasCreated) clientsCreated++;

                RememberClientLocation(clientLocations, client.Client.Id, remote);

                var existing = !string.IsNullOrWhiteSpace(remote.Url)
                    ? await tasks.GetByClickUpUrlAsync(remote.Url, ct)
                    : await tasks.GetByClickUpTaskIdAsync(remote.Id, ct);

                if (existing is null)
                {
                    var task = MapNewTask(remote, client.Client.Id, now);
                    await tasks.InsertAsync(task, ct);
                    tasksCreated++;
                }
                else
                {
                    ApplyApiFields(existing, remote, client.Client.Id, now);
                    await tasks.UpdateApiFieldsAsync(existing, ct);
                    tasksUpdated++;
                }
            }

            if (result.LastPage) break;
            page++;
        }

        await RefreshClientBillFieldsAsync(clientLocations, hierarchyRows, now, ct);

        var summary = $"Synced {tasksCreated + tasksUpdated} tasks ({tasksCreated} new, {tasksUpdated} updated), " +
                      $"{containerEntities.Count} containers, {clientsCreated} new clients.";
        await agencies.UpdateSyncSummaryAsync(agency.Id, now, summary, ct);
        logger.LogInformation("{Summary}", summary);

        return new ClickUpSyncResultDto(now, containerEntities.Count, tasksCreated, tasksUpdated, clientsCreated, summary);
    }

    public async Task<IReadOnlyList<ClickUpHierarchyNodeDto>> GetHierarchyAsync(CancellationToken ct = default)
    {
        var rows = await containers.ListAllAsync(ct);
        var byParent = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ParentExternalId))
            .GroupBy(r => r.ParentExternalId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

        var workspaceChildren = rows
            .Where(r => r.ContainerType == ClickUpHierarchyTypes.Space)
            .OrderBy(r => r.Name)
            .Select(r => BuildNode(r, byParent))
            .ToList();

        return workspaceChildren;
    }

    private static ClickUpHierarchyNodeDto BuildNode(
        ClickUpContainer node, IReadOnlyDictionary<string, List<ClickUpContainer>> byParent)
    {
        var children = byParent.TryGetValue(node.ExternalId, out var kids)
            ? kids.Select(k => BuildNode(k, byParent)).ToList()
            : [];
        return new ClickUpHierarchyNodeDto(node.ContainerType, node.ExternalId, node.Name, children);
    }

    private async Task<(Client Client, bool WasCreated)> EnsureClientAsync(
        Guid agencyId, ClickUpTask remote, DateTimeOffset now, CancellationToken ct)
    {
        var folderId = remote.FolderId;
        var folderName = remote.FolderName ?? "Unknown Client";

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            var existing = await clients.GetByClickUpFolderIdAsync(folderId, ct);
            if (existing is not null)
            {
                var (name, code, original) = ClickUpFolderNaming.Parse(folderName);
                if (existing.Name != name || existing.Code != code)
                {
                    existing.Name = name;
                    existing.Code = code;
                    existing.OriginalName = original;
                    existing.UpdatedAt = now;
                    await clients.UpdateAsync(existing, ct);
                }
                return (existing, false);
            }
        }

        var parsed = ClickUpFolderNaming.Parse(folderName);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            Name = parsed.Name.Length > 0 ? parsed.Name : folderName,
            Code = parsed.Code,
            OriginalName = parsed.OriginalName,
            ClickUpFolderId = folderId,
            Status = ClientStatus.Active,
            Active = true,
            BillFieldAvailable = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return (client, true);
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
        CancellationToken ct)
    {
        var opts = options.Value;
        var spaceByFolder = hierarchy
            .Where(r => r.Type == ClickUpHierarchyTypes.Folder && r.ParentType == ClickUpHierarchyTypes.Space)
            .ToDictionary(r => r.Id, r => r.ParentId, StringComparer.Ordinal);
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

        foreach (var (clientId, hint) in locations)
        {
            var client = await clients.GetByIdAsync(clientId, ct);
            if (client is null) continue;

            try
            {
                var fields = await LoadAccessibleFieldsAsync(hint, spaceByList, spaceByFolder, ct);
                var billable = FindBillableField(fields, opts);
                if (billable is null)
                {
                    client.BillFieldAvailable = false;
                    client.BillCustomFieldId = null;
                    client.BillYesOptionId = null;
                    client.BillNoOptionId = null;
                }
                else
                {
                    client.BillFieldAvailable = true;
                    client.BillCustomFieldId = billable.Id;
                    client.BillYesOptionId = FindOptionId(billable, opts.BillYesOptionId, "yes", "y", "true")
                        ?? opts.BillYesOptionId;
                    client.BillNoOptionId = FindOptionId(billable, opts.BillNoOptionId, "no", "n", "false")
                        ?? opts.BillNoOptionId;
                }

                client.BillFieldCheckedAt = now;
                client.UpdatedAt = now;
                await clients.UpdateAsync(client, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to probe billable custom fields for client {ClientId}", clientId);
                client.BillFieldAvailable = false;
                client.BillFieldCheckedAt = now;
                client.UpdatedAt = now;
                await clients.UpdateAsync(client, ct);
            }
        }
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

    private static WorkTask MapNewTask(ClickUpTask remote, Guid clientId, DateTimeOffset now) =>
        ApplyApiFields(new WorkTask
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            CreatedAt = now,
            UpdatedAt = now
        }, remote, clientId, now);

    private static WorkTask ApplyApiFields(WorkTask task, ClickUpTask remote, Guid clientId, DateTimeOffset now)
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
        task.UpdatedAt = now;
        return task;
    }

    private sealed record ClientLocationHint(string? ListId, string? FolderId, bool FolderHidden);
}
