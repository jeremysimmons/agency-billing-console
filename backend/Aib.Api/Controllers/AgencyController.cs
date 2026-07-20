using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/agency")]
[Authorize]
public sealed class AgencyController(AgencyService agency) : ControllerBase
{
    /// <summary>Returns the current (default top-level) agency for this deployment.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await agency.GetCurrentAsync(ct));

    /// <summary>Updates the current (default top-level) agency.</summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAgencyRequest request, CancellationToken ct)
        => Ok(await agency.UpdateCurrentAsync(request, ct));
}
