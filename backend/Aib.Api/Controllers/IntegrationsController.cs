using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/integrations/clickup")]
[Authorize]
public sealed class IntegrationsController(ClickUpImportService importer, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("connections")]
    public async Task<IActionResult> Connections(CancellationToken ct)
        => Ok(await importer.ListConnectionsAsync(ct));

    /// <summary>Trigger an import. Body: { connectionId?, fullResync }.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] TriggerImportRequest? request, CancellationToken ct)
    {
        var run = await importer.RunImportAsync(
            request?.ConnectionId, request?.FullResync ?? false, currentUser.UserId, ct);
        return Ok(run);
    }

    [HttpGet("imports")]
    public async Task<IActionResult> Imports([FromQuery] Guid? connectionId, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await importer.ListImportsAsync(connectionId, Math.Clamp(limit, 1, 200), ct));
}
