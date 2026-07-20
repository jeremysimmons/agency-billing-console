using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(ClientService clients) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await clients.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await clients.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var created = await clients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
        => Ok(await clients.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await clients.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken ct) => Ok(await clients.DeleteAllAsync(ct));
}

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService projects) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid clientId, CancellationToken ct)
        => Ok(await projects.ListByClientAsync(clientId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
        => Ok(await projects.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(id, request, ct));
}

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? clientId, [FromQuery] bool? missingOnly, CancellationToken ct)
        => Ok(await tasks.ListAsync(clientId, missingOnly, ct));

    [HttpPatch("{id:guid}/prep")]
    public async Task<IActionResult> UpdatePrep(Guid id, [FromBody] UpdateTaskPrepRequest request, CancellationToken ct)
        => Ok(await tasks.UpdatePrepAsync(id, request, ct));
}
