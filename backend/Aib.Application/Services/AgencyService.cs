using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class AgencyService(IAgencyRepository agencies, AccessService access, IClock clock)
{
    public async Task<AgencyDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency is configured.");
        return Map(agency);
    }

    public async Task<AgencyDto> UpdateCurrentAsync(UpdateAgencyRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Agency name is required.");
        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new DomainException("Currency is required.");
        if (request.PaymentTermsDays < 0)
            throw new DomainException("Payment terms must be zero or greater.");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency is configured.");

        agency.Name = request.Name.Trim();
        agency.BillingEmail = string.IsNullOrWhiteSpace(request.BillingEmail) ? null : request.BillingEmail.Trim();
        agency.BillingAddress = string.IsNullOrWhiteSpace(request.BillingAddress) ? null : request.BillingAddress.Trim();
        agency.Currency = request.Currency.Trim().ToUpperInvariant();
        agency.PaymentTermsDays = request.PaymentTermsDays;
        agency.Active = request.Active;
        agency.UpdatedAt = clock.UtcNow;

        await agencies.UpdateAsync(agency, ct);
        return Map(agency);
    }

    private static AgencyDto Map(Agency a) =>
        new(a.Id, a.Name, a.BillingEmail, a.BillingAddress, a.Currency, a.PaymentTermsDays, a.Active);
}
