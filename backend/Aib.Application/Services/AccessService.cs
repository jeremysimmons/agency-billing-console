using Aib.Application.Abstractions;
using Aib.Domain;

namespace Aib.Application.Services;

/// <summary>
/// Access rules: agency and contractor users both see the full agency.
/// Only contractors may manage (create/update/delete) resources.
/// </summary>
public sealed class AccessService(ICurrentUser currentUser)
{
    /// <summary>
    /// Returns null — no per-client restriction; callers scope by agency.
    /// </summary>
    public Task<IReadOnlyCollection<Guid>?> AccessibleClientIdsAsync(CancellationToken _ = default)
    {
        EnsureAuthenticated();
        return Task.FromResult<IReadOnlyCollection<Guid>?>(null);
    }

    public Task EnsureCanViewClientAsync(Guid _, CancellationToken __ = default)
    {
        EnsureAuthenticated();
        return Task.CompletedTask;
    }

    public void EnsureCanManage()
    {
        EnsureAuthenticated();
        if (!currentUser.IsContractorSide)
            throw new ForbiddenException("Only contractor users can modify this resource.");
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Not authenticated.");
    }
}
