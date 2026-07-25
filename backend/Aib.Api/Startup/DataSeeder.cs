using Aib.Application.Abstractions;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Api.Startup;

public sealed class DataSeeder(
    IAgencyRepository agencies,
    IClientRepository clients,
    IClock clock,
    IConfiguration config,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct);
        if (agency is null)
        {
            var now = clock.UtcNow;
            agency = new Agency
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

        var shared = await clients.GetByNameAsync(agency.Id, SharedClients.Name, ct);
        if (shared is null)
        {
            var now = clock.UtcNow;
            shared = new Client
            {
                Id = Guid.NewGuid(),
                AgencyId = agency.Id,
                Name = SharedClients.Name,
                Status = ClientStatus.Active,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await clients.InsertAsync(shared, ct);
            logger.LogInformation("Seeded client {Name}", shared.Name);
        }
    }
}
