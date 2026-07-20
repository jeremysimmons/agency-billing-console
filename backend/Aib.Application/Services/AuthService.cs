using Aib.Application.Abstractions;
using Aib.Application.Contracts;
using Aib.Domain;
using Aib.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Aib.Application.Services;

public sealed class AuthService(
    IUserRepository users,
    ILocalCredentialRepository credentials,
    IMagicLinkRepository magicLinks,
    ISessionRepository sessions,
    IIdentityProviderRepository identityProviders,
    ISocialIdentityRepository socialIdentities,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IGoogleTokenValidator googleValidator,
    IEmailSender email,
    IClock clock,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public async Task<AuthResult> LoginWithPasswordAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalized = Normalize(request.UsernameOrEmail);
        var user = await users.GetByNormalizedUsernameAsync(normalized, ct)
                   ?? await users.GetByNormalizedEmailAsync(normalized, ct);

        if (user is null || user.Status != UserStatus.Active || !user.PasswordLoginEnabled)
            return AuthResult.Fail("Invalid credentials.");

        var credential = await credentials.GetByUserIdAsync(user.Id, ct);
        if (credential is null)
            return AuthResult.Fail("Invalid credentials.");

        if (credential.LockedUntil is { } lockedUntil && lockedUntil > clock.UtcNow)
            return AuthResult.Fail("Account is temporarily locked. Try again later.");

        if (!passwordHasher.Verify(request.Password, credential.PasswordHash))
        {
            credential.FailedAttemptCount++;
            credential.LastFailedAt = clock.UtcNow;
            if (credential.FailedAttemptCount >= _options.MaxFailedLoginAttempts)
            {
                credential.LockedUntil = clock.UtcNow.Add(_options.LockoutDuration);
                credential.FailedAttemptCount = 0;
            }
            await credentials.UpsertAsync(credential, ct);
            return AuthResult.Fail("Invalid credentials.");
        }

        credential.FailedAttemptCount = 0;
        credential.LockedUntil = null;
        await credentials.UpsertAsync(credential, ct);

        return await IssueSessionAsync(user, AuthMethod.Password, null, ct);
    }

    /// <summary>
    /// Always returns without revealing whether the email exists. Sends a link only
    /// when a matching active, magic-link-enabled user exists and the rate limit allows.
    /// </summary>
    public async Task RequestMagicLinkAsync(MagicLinkRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = Normalize(request.Email);
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, ct);

        if (user is null || user.Status != UserStatus.Active || !user.MagicLinkEnabled)
            return;

        var since = clock.UtcNow.Subtract(TimeSpan.FromHours(1));
        var recent = await magicLinks.CountRecentForUserAsync(user.Id, since, ct);
        if (recent >= _options.MagicLinkMaxPerHour)
            return;

        var token = tokens.CreateToken();
        var now = clock.UtcNow;
        await magicLinks.InsertAsync(new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokens.Hash(token),
            Purpose = MagicLinkPurpose.Login,
            RequestedAt = now,
            ExpiresAt = now.Add(_options.MagicLinkLifetime),
            RequestIp = currentUser.IpAddress,
            RequestUserAgent = currentUser.UserAgent,
            CreatedAt = now
        }, ct);

        var link = $"{_options.AppBaseUrl.TrimEnd('/')}/auth/magic-link?token={Uri.EscapeDataString(token)}";
        var body = $"""
            <p>Hello {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
            <p>Use the link below to sign in. It expires in one hour and can be used once.</p>
            <p><a href="{link}">Sign in</a></p>
            """;
        await email.SendAsync(user.Email, "Your sign-in link", body, ct);
    }

    public async Task<AuthResult> ConsumeMagicLinkAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return AuthResult.Fail("Invalid or expired link.");

        var hash = tokens.Hash(token);
        var record = await magicLinks.GetActiveByHashAsync(hash, ct);
        if (record is null || record.ExpiresAt <= clock.UtcNow)
            return AuthResult.Fail("Invalid or expired link.");

        var user = await users.GetByIdAsync(record.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
            return AuthResult.Fail("Invalid or expired link.");

        var now = clock.UtcNow;
        await magicLinks.MarkConsumedAsync(record.Id, now, ct);
        await magicLinks.RevokeAllForUserAsync(user.Id, now, ct);

        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = now;
            await users.UpdateAsync(user, ct);
        }

        return await IssueSessionAsync(user, AuthMethod.MagicLink, null, ct);
    }

    /// <summary>
    /// Authenticate via Google. If the subject is already linked, log that user in.
    /// Otherwise link by exact verified-email match to exactly one active user.
    /// Never auto-creates users.
    /// </summary>
    public async Task<AuthResult> AuthenticateWithGoogleAsync(GoogleLinkRequest request, CancellationToken ct = default)
    {
        var provider = await identityProviders.GetEnabledByTypeAsync("google", ct);
        if (provider is null)
            return AuthResult.Fail("Google sign-in is not configured.");

        GoogleIdentity identity;
        try
        {
            identity = await googleValidator.ValidateAsync(request.IdToken, ct);
        }
        catch (Exception)
        {
            return AuthResult.Fail("Could not validate Google sign-in.");
        }

        if (!identity.EmailVerified)
            return AuthResult.Fail("Your Google email is not verified.");

        if (!string.IsNullOrEmpty(provider.HostedDomain) &&
            !string.Equals(provider.HostedDomain, identity.HostedDomain, StringComparison.OrdinalIgnoreCase))
            return AuthResult.Fail("This Google Workspace domain is not allowed.");

        // Already linked -> log in.
        var existing = await socialIdentities.GetByProviderSubjectAsync(provider.Id, identity.Subject, ct);
        if (existing is not null)
        {
            var linkedUser = await users.GetByIdAsync(existing.UserId, ct);
            if (linkedUser is null || linkedUser.Status != UserStatus.Active)
                return AuthResult.Fail("Account is not active.");
            await socialIdentities.UpdateLastLoginAsync(existing.Id, clock.UtcNow, ct);
            return await IssueSessionAsync(linkedUser, AuthMethod.Google, provider.Id, ct);
        }

        // First-time linking: exact match to a single active user by verified email.
        var normalizedEmail = Normalize(identity.Email);
        var user = await users.GetByNormalizedEmailAsync(normalizedEmail, ct);
        if (user is null || user.Status != UserStatus.Active || !user.SocialLoginEnabled)
            return AuthResult.Fail("No matching account was found for this Google identity.");

        var now = clock.UtcNow;
        await socialIdentities.InsertAsync(new SocialIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            IdentityProviderId = provider.Id,
            ProviderSubject = identity.Subject,
            ProviderEmail = identity.Email,
            NormalizedProviderEmail = normalizedEmail,
            ProviderEmailVerified = identity.EmailVerified,
            HostedDomain = identity.HostedDomain,
            LinkedAt = now,
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);

        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = now;
            await users.UpdateAsync(user, ct);
        }

        return await IssueSessionAsync(user, AuthMethod.Google, provider.Id, ct);
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken ct = default)
    {
        var session = await sessions.GetActiveByHashAsync(tokens.Hash(sessionToken), ct);
        if (session is not null)
            await sessions.RevokeAsync(session.Id, clock.UtcNow, ct);
    }

    public async Task<AuthenticatedUser?> GetCurrentAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return null;
        var roles = await users.GetRoleNamesAsync(userId, ct);
        return ToAuthenticatedUser(user, roles);
    }

    private async Task<AuthResult> IssueSessionAsync(AppUser user, AuthMethod method, Guid? providerId, CancellationToken ct)
    {
        var token = tokens.CreateToken();
        var now = clock.UtcNow;
        var expiresAt = now.Add(_options.SessionLifetime);

        await sessions.InsertAsync(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionTokenHash = tokens.Hash(token),
            AuthenticationMethod = method,
            IdentityProviderId = providerId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            LastSeenAt = now,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent
        }, ct);

        user.LastLoginAt = now;
        await users.UpdateAsync(user, ct);

        var roles = await users.GetRoleNamesAsync(user.Id, ct);
        return AuthResult.Ok(ToAuthenticatedUser(user, roles), token, expiresAt);
    }

    private static AuthenticatedUser ToAuthenticatedUser(AppUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.Username, user.Email, user.DisplayName, roles,
            roles.Contains(Roles.Contractor));
}
