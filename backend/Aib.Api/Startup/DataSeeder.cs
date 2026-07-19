using Aib.Application.Abstractions;
using Aib.Application.Services;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Api.Startup;

/// <summary>Seeds roles, the single agency/contractor, the owner user, and the Google provider.</summary>
public sealed class DataSeeder(
    IRoleRepository roles,
    IAgencyRepository agencies,
    IContractorRepository contractors,
    IUserRepository users,
    ILocalCredentialRepository credentials,
    IIdentityProviderRepository identityProviders,
    IPasswordHasher passwordHasher,
    IClock clock,
    IConfiguration config,
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
            await SeedOwnerAsync(agency, ownerEmail!, ownerPassword!, ct);

        await SeedGoogleProviderAsync(ct);
    }

    private async Task SeedOwnerAsync(Agency agency, string email, string password, CancellationToken ct)
    {
        var normalizedEmail = AuthService.Normalize(email);
        if (await users.GetByNormalizedEmailAsync(normalizedEmail, ct) is not null)
            return;

        var now = clock.UtcNow;
        var name = config["Seed:Owner:DisplayName"] ?? "Owner";
        var username = config["Seed:Owner:Username"] ?? email;

        var contractor = await contractors.GetByEmailAsync(email, ct);
        Guid contractorId;
        if (contractor is null)
        {
            contractorId = Guid.NewGuid();
            await contractors.InsertAsync(new Contractor
            {
                Id = contractorId, Name = name, Email = email,
                DefaultHourlyRate = decimal.TryParse(config["Seed:Contractor:DefaultHourlyRate"], out var rate) ? rate : null,
                Active = true, CreatedAt = now, UpdatedAt = now
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
            DisplayName = name,
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

        logger.LogInformation("Seeded owner user {Email}", email);
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
