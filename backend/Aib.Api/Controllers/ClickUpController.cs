using System.Text.Json;
using Aib.Application.Contracts;
using Aib.Application.Services;
using Aib.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Aib.Api.Controllers;

[ApiController]
[Route("api/agency")]
public sealed class AgencyController(AgencyService agency) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await agency.GetAsync(ct));

    [HttpPut("ui-preferences")]
    public async Task<IActionResult> UpdateUiPreferences(
        [FromBody] UpdateAgencyUiPreferencesRequest request,
        CancellationToken ct)
        => Ok(await agency.UpdateUiPreferencesAsync(request, ct));
}

[ApiController]
[Route("api/clickup")]
public sealed class ClickUpController(ClickUpSyncService sync, CsvTaskImportService csvImport) : ControllerBase
{
    private static readonly JsonSerializerOptions SyncEventJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [HttpPost("sync")]
    public async Task Sync(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        await Response.StartAsync(ct);

        async Task Report(ClickUpSyncProgressEvent evt, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(evt, SyncEventJson);
            await Response.WriteAsync($"event: sync\ndata: {json}\n\n", token);
            await Response.Body.FlushAsync(token);
        }

        try
        {
            await sync.SyncAsync(Report, ct);
        }
        catch (DomainException ex)
        {
            await Report(new ClickUpSyncProgressEvent("error", Error: ex.Message), ct);
        }
        catch (Exception)
        {
            await Report(new ClickUpSyncProgressEvent("error", Error: "Sync failed."), ct);
        }
    }

    [HttpGet("hierarchy")]
    public async Task<IActionResult> Hierarchy(CancellationToken ct) => Ok(await sync.GetHierarchyAsync(ct));

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "CSV file is required." });

        await using var stream = file.OpenReadStream();
        return Ok(await csvImport.ImportAsync(stream, ct));
    }
}
