using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

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
