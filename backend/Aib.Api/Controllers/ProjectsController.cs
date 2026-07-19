using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ProjectsController(ProjectService projects) : ControllerBase
{
    /// <summary>List projects for a client: GET /api/projects?clientId=...</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid clientId, CancellationToken ct)
        => Ok(await projects.ListByClientAsync(clientId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await projects.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var created = await projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(id, request, ct));
}
