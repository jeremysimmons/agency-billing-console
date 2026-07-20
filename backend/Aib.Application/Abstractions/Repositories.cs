using Aib.Domain;
using Aib.Domain.Entities;

namespace Aib.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct = default);
    Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<Guid> InsertAsync(AppUser user, CancellationToken ct = default);
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
    Task AddRoleAsync(Guid userId, int roleId, CancellationToken ct = default);
}

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task EnsureSeededAsync(IEnumerable<string> names, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
}

public interface ILocalCredentialRepository
{
    Task<LocalCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(LocalCredential credential, CancellationToken ct = default);
}

public interface IMagicLinkRepository
{
    Task InsertAsync(MagicLinkToken token, CancellationToken ct = default);
    Task<MagicLinkToken?> GetActiveByHashAsync(string tokenHash, CancellationToken ct = default);
    Task MarkConsumedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset when, CancellationToken ct = default);
    Task<int> CountRecentForUserAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default);
    Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken ct = default);
}

public interface ISessionRepository
{
    Task InsertAsync(UserSession session, CancellationToken ct = default);
    Task<UserSession?> GetActiveByHashAsync(string tokenHash, CancellationToken ct = default);
    Task TouchAsync(Guid id, DateTimeOffset when, CancellationToken ct = default);
    Task RevokeAsync(Guid id, DateTimeOffset when, CancellationToken ct = default);
    Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken ct = default);
}

public interface IIdentityProviderRepository
{
    Task<IdentityProvider?> GetEnabledByTypeAsync(string providerType, CancellationToken ct = default);
    Task<IdentityProvider?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertGoogleAsync(IdentityProvider provider, CancellationToken ct = default);
}

public interface ISocialIdentityRepository
{
    Task<SocialIdentity?> GetByProviderSubjectAsync(Guid providerId, string subject, CancellationToken ct = default);
    Task<SocialIdentity?> GetByUserAndProviderAsync(Guid userId, Guid providerId, CancellationToken ct = default);
    Task InsertAsync(SocialIdentity identity, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid id, DateTimeOffset when, CancellationToken ct = default);
}

public interface IAuthEventRepository
{
    Task InsertAsync(AuthEvent authEvent, CancellationToken ct = default);
}

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListAsync(Guid agencyId, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default);
    Task<Guid> InsertAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
}

public interface IClientAccessRepository
{
    Task<IReadOnlyList<Guid>> GetAccessibleClientIdsAsync(Guid userId, CancellationToken ct = default);
    Task GrantAsync(ClientAccess access, CancellationToken ct = default);
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<Guid> InsertAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
}

public interface ITaskRepository
{
    Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> ListByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> ListByWorkStatusAsync(IReadOnlyCollection<WorkStatus> statuses, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> ListByBillingStatusAsync(BillingStatus status, IReadOnlyCollection<Guid>? restrictToClientIds, CancellationToken ct = default);
    Task<Guid> InsertAsync(WorkTask task, CancellationToken ct = default);
    Task UpdateAsync(WorkTask task, CancellationToken ct = default);
    /// <summary>Return the ancestor ids of a task using a recursive CTE (excludes the task itself).</summary>
    Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> GetSubtreeAsync(Guid rootTaskId, CancellationToken ct = default);
}

public interface IAgencyRepository
{
    Task<Agency?> GetDefaultAsync(CancellationToken ct = default);
    Task<Guid> InsertAsync(Agency agency, CancellationToken ct = default);
}

public interface IContractorRepository
{
    Task<Contractor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contractor?> GetDefaultAsync(CancellationToken ct = default);
    Task<Contractor?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> InsertAsync(Contractor contractor, CancellationToken ct = default);
}

public interface ITimeEntryRepository
{
    Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TimeEntry>> ListByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TimeEntry>> ListByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<int> SumDurationMinutesAsync(Guid taskId, bool directOnly, CancellationToken ct = default);
    Task<Guid> InsertAsync(TimeEntry entry, CancellationToken ct = default);
    Task UpdateAsync(TimeEntry entry, CancellationToken ct = default);
}

public interface ITimeEntrySourceRepository
{
    Task<TimeEntrySource?> GetByExternalIdAsync(Guid externalTimeEntryId, CancellationToken ct = default);
    Task<TimeEntrySource?> GetByTimeEntryIdAsync(Guid timeEntryId, CancellationToken ct = default);
    Task InsertAsync(TimeEntrySource source, CancellationToken ct = default);
}

public interface IExternalTimeEntryQueryRepository
{
    Task<IReadOnlyList<ExternalTimeEntry>> ListUnlinkedForMappedTasksAsync(Guid connectionId, CancellationToken ct = default);
    Task<ExternalTimeEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
