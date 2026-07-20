using Aib.Application;
using Aib.Application.Abstractions;
using Aib.Application.Services;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Aib.Api.Startup;

/// <summary>Seeds roles, agency/contractor, contractor users, Google provider, and ClickUp connection.</summary>
public sealed class DataSeeder(
    IRoleRepository roles,
    IAgencyRepository agencies,
    IContractorRepository contractors,
    IUserRepository users,
    ILocalCredentialRepository credentials,
    IIdentityProviderRepository identityProviders,
    IExternalConnectionRepository externalConnections,
    IPasswordHasher passwordHasher,
    IClock clock,
    IConfiguration config,
    IOptions<ClickUpOptions> clickUpOptions,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await roles.EnsureSeededAsync(Roles.All, ct);

        var agency = await agencies.GetDefaultAsync(ct);
        if (agency is null)
        {
            var now = clock.UtcNow;
            agency = new Agency
            {
                Id = Guid.NewGuid(),
                Name = config["Seed:Agency:Name"] ?? "Default Agency",
                BillingEmail = config["Seed:Agency:BillingEmail"],
                Currency = config["Seed:Agency:Currency"] ?? "USD",
                CreatedAt = now,
                UpdatedAt = now
            };
            await agencies.InsertAsync(agency, ct);
            logger.LogInformation("Seeded agency {Name}", agency.Name);
        }

        var ownerEmail = config["Seed:Owner:Email"];
        var ownerPassword = config["Seed:Owner:Password"];
        if (!string.IsNullOrWhiteSpace(ownerEmail) && !string.IsNullOrWhiteSpace(ownerPassword))
        {
            await SeedContractorAdminAsync(
                agency,
                email: ownerEmail!,
                password: ownerPassword!,
                username: config["Seed:Owner:Username"] ?? "owner",
                displayName: config["Seed:Owner:DisplayName"] ?? "Owner",
                ct);
        }

        foreach (var user in config.GetSection("Seed:Users").GetChildren())
        {
            var email = user["Email"];
            var password = user["Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                continue;

            await SeedContractorAdminAsync(
                agency,
                email: email!,
                password: password!,
                username: user["Username"] ?? email!,
                displayName: user["DisplayName"] ?? email!,
                ct);
        }

        await SeedGoogleProviderAsync(ct);
        await SeedClickUpConnectionAsync(agency, ct);
    }

    private async Task SeedClickUpConnectionAsync(Agency agency, CancellationToken ct)
    {
        var teamId = clickUpOptions.Value.TeamId;
        if (string.IsNullOrWhiteSpace(teamId))
            return;

        if (await externalConnections.GetByProviderWorkspaceAsync("clickup", teamId, ct) is not null)
            return;

        var now = clock.UtcNow;
        await externalConnections.InsertAsync(new ExternalConnection
        {
            Id = Guid.NewGuid(),
            AgencyId = agency.Id,
            ProviderType = "clickup",
            Name = "ClickUp",
            ExternalWorkspaceId = teamId,
            AuthenticationReference = "env:CLICKUP_API_TOKEN",
            Status = ExternalConnectionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
        logger.LogInformation("Seeded ClickUp connection for workspace {TeamId}", teamId);
    }

    private async Task SeedContractorAdminAsync(
        Agency agency, string email, string password, string username, string displayName, CancellationToken ct)
    {
        var normalizedEmail = AuthService.Normalize(email);
        if (await users.GetByNormalizedEmailAsync(normalizedEmail, ct) is not null)
            return;

        var now = clock.UtcNow;

        // Prefer the existing default contractor so multiple admins share one contractor record.
        var contractor = await contractors.GetDefaultAsync(ct)
                         ?? await contractors.GetByEmailAsync(email, ct);
        Guid contractorId;
        if (contractor is null)
        {
            contractorId = Guid.NewGuid();
            await contractors.InsertAsync(new Contractor
            {
                Id = contractorId,
                Name = displayName,
                Email = email,
                DefaultHourlyRate = decimal.TryParse(config["Seed:Contractor:DefaultHourlyRate"], out var rate) ? rate : null,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }
        else contractorId = contractor.Id;

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            AgencyId = agency.Id,
            ContractorId = contractorId,
            Username = username,
            NormalizedUsername = AuthService.Normalize(username),
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName,
            Status = UserStatus.Active,
            EmailVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        await users.InsertAsync(user, ct);

        await credentials.UpsertAsync(new LocalCredential
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PasswordHash = passwordHasher.Hash(password),
            PasswordChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);

        var role = await roles.GetByNameAsync(Roles.ContractorAdmin, ct);
        if (role is not null)
            await users.AddRoleAsync(user.Id, role.Id, ct);

        logger.LogInformation("Seeded contractor admin {Email} ({Username})", email, username);
    }

    private async Task SeedGoogleProviderAsync(CancellationToken ct)
    {
        var clientId = config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return;

        var now = clock.UtcNow;
        await identityProviders.UpsertGoogleAsync(new IdentityProvider
        {
            Id = Guid.NewGuid(),
            ProviderType = "google",
            Name = "Google Workspace",
            Issuer = "https://accounts.google.com",
            ClientId = clientId!,
            SecretReference = config["Google:SecretReference"],
            HostedDomain = config["Google:HostedDomain"],
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
        logger.LogInformation("Seeded Google identity provider");
    }
}
