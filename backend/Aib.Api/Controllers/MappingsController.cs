using Aib.Application.Contracts;
using Aib.Application.Services;
using Aib.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/integrations/clickup")]
[Authorize]
public sealed class MappingsController(MappingService mappings) : ControllerBase
{
    [HttpGet("unmapped/containers")]
    public async Task<IActionResult> UnmappedContainers([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ListUnmappedContainersAsync(connectionId, ct));

    [HttpGet("unmapped/tasks")]
    public async Task<IActionResult> UnmappedTasks([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ListUnmappedWorkItemsAsync(connectionId, ct));

    [HttpGet("mappings/containers")]
    public async Task<IActionResult> ContainerMappings([FromQuery] Guid? connectionId, [FromQuery] MappingStatus? status, CancellationToken ct)
        => Ok(await mappings.ListContainerMappingsAsync(connectionId, status, ct));

    [HttpGet("mappings/statuses")]
    public async Task<IActionResult> StatusMappings([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ListStatusMappingsAsync(connectionId, ct));

    [HttpPost("mappings/suggest")]
    public async Task<IActionResult> Suggest([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.SuggestAsync(connectionId, ct));

    [HttpPost("mappings/import-folders")]
    public async Task<IActionResult> ImportFolders([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ImportFoldersAsClientsAsync(connectionId, ct));

    [HttpPost("mappings/import-lists")]
    public async Task<IActionResult> ImportLists([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ImportListsAsProjectsAsync(connectionId, ct));

    [HttpPost("mappings/containers/{containerId:guid}/confirm")]
    public async Task<IActionResult> ConfirmContainer(Guid containerId, [FromBody] ConfirmContainerMappingRequest request, CancellationToken ct)
        => Ok(await mappings.ConfirmContainerAsync(containerId, request, ct));

    [HttpPost("mappings/containers/{containerId:guid}/ignore")]
    public async Task<IActionResult> IgnoreContainer(Guid containerId, [FromBody] IgnoreMappingRequest? request, CancellationToken ct)
    {
        await mappings.IgnoreContainerAsync(containerId, request ?? new IgnoreMappingRequest(null), ct);
        return Ok(new { ok = true });
    }

    [HttpPost("mappings/tasks/{workItemId:guid}/confirm")]
    public async Task<IActionResult> ConfirmTask(Guid workItemId, [FromBody] ConfirmTaskMappingRequest request, CancellationToken ct)
        => Ok(await mappings.ConfirmTaskAsync(workItemId, request, ct));

    [HttpPost("mappings/tasks/{workItemId:guid}/ignore")]
    public async Task<IActionResult> IgnoreTask(Guid workItemId, [FromBody] IgnoreMappingRequest? request, CancellationToken ct)
    {
        await mappings.IgnoreTaskAsync(workItemId, request ?? new IgnoreMappingRequest(null), ct);
        return Ok(new { ok = true });
    }

    [HttpPut("mappings/statuses")]
    public async Task<IActionResult> UpsertStatus([FromQuery] Guid? connectionId, [FromBody] UpsertStatusMappingRequest request, CancellationToken ct)
        => Ok(await mappings.UpsertStatusMappingAsync(connectionId, request, ct));

    [HttpPost("mappings/apply-statuses")]
    public async Task<IActionResult> ApplyStatuses([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await mappings.ApplyStatusesAsync(connectionId, ct));
}
