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

        while (true)
        {
            var result = await clickUp.GetTasksAsync(teamId, opts.AssigneeId, page, ct);
            foreach (var remote in result.Tasks)
            {
                var client = await EnsureClientAsync(agency.Id, remote, now, ct);
                if (client.WasCreated) clientsCreated++;

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
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return (client, true);
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
}
