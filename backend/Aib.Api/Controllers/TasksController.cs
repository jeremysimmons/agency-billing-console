using Aib.Application.Contracts;
using Aib.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    /// <summary>List tasks for a client: GET /api/tasks?clientId=...</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid clientId, CancellationToken ct)
        => Ok(await tasks.ListByClientAsync(clientId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await tasks.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateAsync(id, request, ct));
}
