using Aib.Application.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aib.Infrastructure.Integrations;

/// <summary>Scheduled incremental ClickUp import.</summary>
[DisallowConcurrentExecution]
public sealed class ClickUpImportJob(ClickUpImportService importer, ILogger<ClickUpImportJob> logger) : IJob
{
    public static readonly JobKey Key = new("clickup-incremental-import");

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var run = await importer.RunImportAsync(connectionId: null, full: false, triggeredByUserId: null, context.CancellationToken);
            logger.LogInformation("Scheduled ClickUp import {Status}: fetched {Fetched}, created {Created}, updated {Updated}, unchanged {Unchanged}, failed {Failed}",
                run.Status, run.RecordsFetched, run.RecordsCreated, run.RecordsUpdated, run.RecordsUnchanged, run.RecordsFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled ClickUp import threw");
        }
    }
}
