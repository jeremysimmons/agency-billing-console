namespace Aib.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; }
    public Guid? AgencyId { get; set; }
    public Guid? ContractorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Invited;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public bool PasswordLoginEnabled { get; set; } = true;
    public bool MagicLinkEnabled { get; set; } = true;
    public bool SocialLoginEnabled { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ClientAccess
{
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public AccessLevel AccessLevel { get; set; } = AccessLevel.View;
}

public class LocalCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }
    public int FailedAttemptCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class MagicLinkToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public MagicLinkPurpose Purpose { get; set; } = MagicLinkPurpose.Login;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RequestIp { get; set; }
    public string? RequestUserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class IdentityProvider
{
    public Guid Id { get; set; }
    public string ProviderType { get; set; } = "google";
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? SecretReference { get; set; }
    public string? HostedDomain { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class SocialIdentity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid IdentityProviderId { get; set; }
    public string ProviderSubject { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string NormalizedProviderEmail { get; set; } = string.Empty;
    public bool ProviderEmailVerified { get; set; }
    public string? HostedDomain { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
    public AuthMethod AuthenticationMethod { get; set; }
    public Guid? IdentityProviderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class AuthEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public AuthMethod? AuthenticationMethod { get; set; }
    public bool Success { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
