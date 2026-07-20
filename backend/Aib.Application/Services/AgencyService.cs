using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class AgencyService(IAgencyRepository agencies)
{
    public async Task<AgencyDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency is configured.");
        return Map(agency);
    }

    private static AgencyDto Map(Agency a) =>
        new(a.Id, a.Name, a.BillingEmail, a.Currency, a.PaymentTermsDays, a.Active);
}
