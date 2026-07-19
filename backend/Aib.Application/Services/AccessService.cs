using Aib.Application.Abstractions;

namespace Aib.Application.Services;

/// <summary>
/// Centralizes client-scoping. Contractor-side users have unrestricted access;
/// agency users are limited to their <c>client_access</c> grants.
/// </summary>
public sealed class AccessService(ICurrentUser currentUser, IClientAccessRepository clientAccess)
{
    /// <summary>
    /// Returns null when the caller may see every client (contractor side),
    /// otherwise the explicit set of accessible client ids.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>?> AccessibleClientIdsAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Not authenticated.");

        if (currentUser.IsContractorSide)
            return null;

        return await clientAccess.GetAccessibleClientIdsAsync(currentUser.UserId!.Value, ct);
    }

    public async Task EnsureCanViewClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var accessible = await AccessibleClientIdsAsync(ct);
        if (accessible is not null && !accessible.Contains(clientId))
            throw new ForbiddenException("You do not have access to this client.");
    }

    /// <summary>Managing clients/projects/tasks is contractor-side only in Phase 1.</summary>
    public void EnsureCanManage()
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Not authenticated.");
        if (!currentUser.IsContractorSide)
            throw new ForbiddenException("Only contractor users can modify this resource.");
    }
}
