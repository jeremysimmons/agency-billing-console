using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/time-entries")]
[Authorize]
public sealed class TimeEntriesController(TimeService time) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListByTask([FromQuery] Guid taskId, CancellationToken ct)
        => Ok(await time.ListByTaskAsync(taskId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTimeEntryRequest request, CancellationToken ct)
        => Ok(await time.CreateAsync(request, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => Ok(await time.ApproveAsync(id, ct));

    [HttpPost("sync-imported")]
    public async Task<IActionResult> SyncImported([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await time.SyncImportedAsync(connectionId, ct));
}

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TaskRollupsController(RollupService rollups) : ControllerBase
{
    [HttpGet("{id:guid}/rollup")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await rollups.GetAsync(id, ct));

    [HttpGet("rollups")]
    public async Task<IActionResult> ByClient([FromQuery] Guid clientId, CancellationToken ct)
        => Ok(await rollups.ListByClientAsync(clientId, ct));
}

[ApiController]
[Route("api/work")]
[Authorize]
public sealed class WorkController(WorkReviewService work) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await work.ListPendingAsync(ct));

    [HttpGet("completed")]
    public async Task<IActionResult> Completed(CancellationToken ct) => Ok(await work.ListCompletedAsync(ct));

    [HttpGet("finalized")]
    public async Task<IActionResult> Finalized(CancellationToken ct) => Ok(await work.ListFinalizedAsync(ct));

    [HttpPost("{taskId:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid taskId, [FromBody] FinalizeWorkRequest? request, CancellationToken ct)
        => Ok(await work.FinalizeAsync(taskId, request, ct));

    [HttpPost("{taskId:guid}/exclude")]
    public async Task<IActionResult> Exclude(Guid taskId, [FromBody] ExcludeWorkRequest? request, CancellationToken ct)
        => Ok(await work.ExcludeAsync(taskId, request, ct));
}
