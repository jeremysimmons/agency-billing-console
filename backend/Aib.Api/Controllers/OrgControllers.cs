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
    public async Task<IActionResult> List(
        [FromQuery] Guid? clientId,
        [FromQuery] bool includeShared = false,
        CancellationToken ct = default)
    {
        if (clientId is null)
            return Ok(await projects.ListAsync(ct));
        return Ok(await projects.ListByClientAsync(clientId.Value, includeShared, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
        => Ok(await projects.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(id, request, ct));
}

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController(InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await invoices.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken ct)
        => Ok(await invoices.CreateAsync(request, ct));

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderInvoicesRequest request, CancellationToken ct)
        => Ok(await invoices.ReorderAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
        => Ok(await invoices.UpdateAsync(id, request, ct));
}

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks, ClickUpSyncService sync) : ControllerBase
{
    [HttpGet("filter-options")]
    public async Task<IActionResult> FilterOptions([FromQuery] Guid? clientId, CancellationToken ct)
        => Ok(await tasks.GetFilterOptionsAsync(clientId, ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] Guid? clientId,
        [FromQuery] bool? missingOnly,
        [FromQuery] string[]? invoiced,
        [FromQuery] Guid? projectId,
        [FromQuery] bool? unassignedOnly,
        [FromQuery] string? createdMonth,
        [FromQuery] string? doneMonth,
        [FromQuery] string[]? statuses,
        [FromQuery] string? listId,
        [FromQuery] string? folderId,
        [FromQuery] string? spaceId,
        [FromQuery] string? invoiceLabel,
        CancellationToken ct)
        => Ok(await tasks.GetSummaryAsync(
            clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            listId, folderId, spaceId, invoiceLabel, ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? clientId,
        [FromQuery] bool? missingOnly,
        [FromQuery] string[]? invoiced,
        [FromQuery] Guid? projectId,
        [FromQuery] bool? unassignedOnly,
        [FromQuery] string? createdMonth,
        [FromQuery] string? doneMonth,
        [FromQuery] string[]? statuses,
        [FromQuery] string? listId,
        [FromQuery] string? folderId,
        [FromQuery] string? spaceId,
        [FromQuery] string? invoiceLabel,
        CancellationToken ct)
        => Ok(await tasks.ListAsync(
            clientId, missingOnly, invoiced, projectId, unassignedOnly, createdMonth, doneMonth, statuses,
            listId, folderId, spaceId, invoiceLabel, ct));

    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct)
        => Ok(await sync.SyncTaskAsync(id, ct));

    [HttpPatch("{id:guid}/bill")]
    public async Task<IActionResult> UpdateBill(Guid id, [FromBody] UpdateTaskBillRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateBillAsync(id, request.Bill, ct));

    [HttpPatch("{id:guid}/project")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateTaskProjectRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateProjectAsync(id, request.ProjectId, ct));

    [HttpPatch("{id:guid}/invoice")]
    public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateTaskInvoiceRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateInvoiceAsync(id, request.InvoiceLabel, ct));

    [HttpPatch("{id:guid}/discount")]
    public async Task<IActionResult> UpdateDiscount(Guid id, [FromBody] UpdateTaskDiscountRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateDiscountAsync(id, request.DiscountPercent, ct));

    [HttpPatch("{id:guid}/billable-hours")]
    public async Task<IActionResult> UpdateBillableHours(Guid id, [FromBody] UpdateTaskHoursRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateBillableHoursAsync(id, request.Hours, ct));

    [HttpPatch("{id:guid}/non-billable-hours")]
    public async Task<IActionResult> UpdateNonBillableHours(Guid id, [FromBody] UpdateTaskHoursRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateNonBillableHoursAsync(id, request.Hours, ct));

    [HttpPatch("{id:guid}/prep")]
    public async Task<IActionResult> UpdatePrep(Guid id, [FromBody] UpdateTaskPrepRequest request, CancellationToken ct)
        => Ok(await tasks.UpdatePrepAsync(id, request, ct));
}
