using Aib.Application.Abstractions;
using Aib.Domain.Entities;

namespace Aib.Api.Startup;

public sealed class DataSeeder(IAgencyRepository agencies, IClock clock, IConfiguration config, ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await agencies.GetDefaultAsync(ct) is not null)
            return;

        var now = clock.UtcNow;
        var agency = new Agency
        {
            Id = Guid.NewGuid(),
            Name = config["Seed:Agency:Name"] ?? "12 Legs Billing Prep",
            Currency = config["Seed:Agency:Currency"] ?? "USD",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await agencies.InsertAsync(agency, ct);
        logger.LogInformation("Seeded agency {Name}", agency.Name);
    }
}
