using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

/// <summary>Computes estimate and actual rollups from source records (no cached totals).</summary>
public sealed class RollupService(
    ITaskRepository tasks,
    ITimeEntryRepository timeEntries,
    AccessService access)
{
    public async Task<TaskRollupDto> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetByIdAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        await access.EnsureCanViewClientAsync(task.ClientId, ct);

        var tree = await tasks.GetSubtreeAsync(taskId, ct);
        var descendants = tree.Where(t => t.Id != taskId).ToList();

        var directActual = await timeEntries.SumDurationMinutesAsync(taskId, directOnly: true, ct);
        var subtreeActual = await timeEntries.SumDurationMinutesAsync(taskId, directOnly: false, ct);
        var childrenActual = subtreeActual - directActual;

        var directEstimate = task.EstimatedMinutes ?? 0;
        var childrenEstimate = descendants.Sum(d => d.EstimatedMinutes ?? 0);

        return new TaskRollupDto(
            task.Id, task.Title, task.EstimateRollupMode, task.ActualRollupMode,
            task.EstimatedMinutes,
            ApplyMode(task.EstimateRollupMode, directEstimate, childrenEstimate),
            directActual,
            ApplyMode(task.ActualRollupMode, directActual, childrenActual),
            descendants.Count);
    }

    public async Task<IReadOnlyList<TaskRollupDto>> ListByClientAsync(Guid clientId, CancellationToken ct = default)
    {
        await access.EnsureCanViewClientAsync(clientId, ct);
        var list = await tasks.ListByClientAsync(clientId, ct);
        var result = new List<TaskRollupDto>();
        foreach (var t in list)
            result.Add(await GetAsync(t.Id, ct));
        return result;
    }

    private static int ApplyMode(RollupMode mode, int direct, int children) => mode switch
    {
        RollupMode.Direct => direct,
        RollupMode.Children => children,
        RollupMode.DirectAndChildren => direct + children,
        _ => direct
    };
}
