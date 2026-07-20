using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Services;

public sealed class ClientService(
    IClientRepository clients,
    IAgencyRepository agencies,
    AccessService access,
    IClock clock)
{
    public async Task<IReadOnlyList<ClientDto>> ListAsync(CancellationToken ct = default)
    {
        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency is configured.");
        var accessible = await access.AccessibleClientIdsAsync(ct);
        var list = await clients.ListAsync(agency.Id, accessible, ct);
        return list.Select(Map).ToList();
    }

    public async Task<ClientDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await access.EnsureCanViewClientAsync(id, ct);
        var client = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        return Map(client);
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Client name is required.");

        var agency = await agencies.GetDefaultAsync(ct)
                     ?? throw new NotFoundException("No agency is configured.");
        var now = clock.UtcNow;
        var client = new Client
        {
            Id = Guid.NewGuid(),
            AgencyId = agency.Id,
            Name = request.Name.Trim(),
            Code = request.Code?.Trim(),
            Description = request.Description,
            Status = request.Status ?? ClientStatus.Active,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await clients.InsertAsync(client, ct);
        return Map(client);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        var client = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Client name is required.");

        client.Name = request.Name.Trim();
        client.Code = request.Code?.Trim();
        client.Description = request.Description;
        client.Status = request.Status;
        client.Active = request.Active;
        client.UpdatedAt = clock.UtcNow;
        await clients.UpdateAsync(client, ct);
        return Map(client);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        access.EnsureCanManage();
        _ = await clients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Client not found.");
        await clients.DeleteAsync(id, ct);
    }

    private static ClientDto Map(Client c) =>
        new(c.Id, c.Name, c.Code, c.Description, c.Status, c.Active);
}
