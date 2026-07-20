using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Application.Integrations;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Aib.Application.Services;

public sealed class CsvTaskImportService(
    IAgencyRepository agencies,
    IClientRepository clients,
    ITaskRepository tasks,
    IClock clock,
    ILogger<CsvTaskImportService> logger)
{
    public async Task<CsvImportResultDto> ImportAsync(Stream csvStream, CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency configured.");
        var now = clock.UtcNow;

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        });

        var imported = 0;
        var updated = 0;
        var skipped = 0;

        await foreach (var row in csv.GetRecordsAsync<CsvTaskRow>(ct))
        {
            if (string.IsNullOrWhiteSpace(row.Url)) { skipped++; continue; }

            var client = await EnsureClientAsync(agency.Id, row.ProjectId, row.ProjectName ?? "Unknown Client", now, ct);

            var existing = await tasks.GetByClickUpUrlAsync(row.Url, ct)
                           ?? (ExtractTaskId(row.Url) is { } tid
                               ? await tasks.GetByClickUpTaskIdAsync(tid, ct)
                               : null);

            if (existing is null)
            {
                var task = MapFromCsv(row, client.Id, now);
                task.ClickUpUrl = row.Url;
                await tasks.InsertAsync(task, ct);
                imported++;
            }
            else
            {
                MergeCsv(existing, row, client.Id, now);
                await tasks.UpdateAsync(existing, ct);
                updated++;
            }
        }

        var summary = $"CSV import: {imported} created, {updated} updated, {skipped} skipped.";
        logger.LogInformation("{Summary}", summary);
        return new CsvImportResultDto(imported, updated, skipped, summary);
    }

    private sealed class CsvTaskRow
    {
        public string Url { get; set; } = string.Empty;
        public string? Bill { get; set; }
        [Name("Billable Hours")]
        public string? BillableHours { get; set; }
        [Name("NonBillable Hours")]
        public string? NonBillableHours { get; set; }
        public string? Invoice { get; set; }
        public string? Note { get; set; }
        [Name("project_name")]
        public string? ProjectName { get; set; }
        [Name("project_id")]
        public string? ProjectId { get; set; }
        [Name("list_name")]
        public string? ListName { get; set; }
        [Name("list_id")]
        public string? ListId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Parent { get; set; }
        public string? Status { get; set; }
        public string? Tags { get; set; }
        [Name("date_created")]
        public string? DateCreated { get; set; }
        [Name("due_date")]
        public string? DueDate { get; set; }
        [Name("date_done")]
        public string? DateDone { get; set; }
        [Name("date_closed")]
        public string? DateClosed { get; set; }
        [Name("order_index")]
        public string? OrderIndex { get; set; }
        [Name("estimated_hours")]
        public string? EstimatedHours { get; set; }
        [Name("actual_hours")]
        public string? ActualHours { get; set; }
    }

    private async Task<Client> EnsureClientAsync(
        Guid agencyId, string? folderId, string folderName, DateTimeOffset now, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(folderId))
        {
            var byFolder = await clients.GetByClickUpFolderIdAsync(folderId, ct);
            if (byFolder is not null) return byFolder;
        }

        var parsed = ClickUpFolderNaming.Parse(folderName);
        var client = new Client
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            Name = parsed.Name.Length > 0 ? parsed.Name : folderName,
            Code = parsed.Code,
            OriginalName = parsed.OriginalName,
            ClickUpFolderId = folderId,
            Status = ClientStatus.Active,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return client;
    }

    private static WorkTask MapFromCsv(CsvTaskRow row, Guid clientId, DateTimeOffset now)
    {
        var task = new WorkTask { Id = Guid.NewGuid(), ClientId = clientId, CreatedAt = now, UpdatedAt = now };
        MergeCsv(task, row, clientId, now);
        return task;
    }

    private static void MergeCsv(WorkTask task, CsvTaskRow row, Guid clientId, DateTimeOffset now)
    {
        task.ClientId = clientId;
        task.Bill = NullIfEmpty(row.Bill);
        task.BillableHours = ParseDecimal(row.BillableHours);
        task.NonBillableHours = ParseDecimal(row.NonBillableHours);
        task.InvoiceLabel = NullIfEmpty(row.Invoice);
        task.Note = NullIfEmpty(row.Note);
        task.ClickUpTaskId = ExtractTaskId(row.Url);
        task.ClickUpFolderId = NullIfEmpty(row.ProjectId);
        task.ClickUpFolderName = NullIfEmpty(row.ProjectName);
        task.ClickUpListId = NullIfEmpty(row.ListId);
        task.ClickUpListName = NullIfEmpty(row.ListName);
        task.Title = row.Name ?? "(untitled)";
        task.Description = NullIfEmpty(row.Description);
        task.ClickUpParentId = NullIfEmpty(row.Parent);
        task.ClickUpStatus = NullIfEmpty(row.Status);
        task.Tags = NullIfEmpty(row.Tags);
        task.DateCreated = ParseDate(row.DateCreated);
        task.DueDate = ParseDate(row.DueDate);
        task.DateDone = ParseDate(row.DateDone);
        task.DateClosed = ParseDate(row.DateClosed);
        task.OrderIndex = long.TryParse(row.OrderIndex, out var oi) ? oi : null;
        task.EstimatedHours = ParseDecimal(row.EstimatedHours);
        task.ActualHours = ParseDecimal(row.ActualHours);
        task.UpdatedAt = now;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto
            : null;
    }

    private static string? ExtractTaskId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var idx = url.LastIndexOf("/t/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        return url[(idx + 3)..].Trim('/');
    }
}
