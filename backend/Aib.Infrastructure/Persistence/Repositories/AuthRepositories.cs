using Aib.Application.Abstractions;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class LocalCredentialRepository(IDbConnectionFactory factory) : ILocalCredentialRepository
{
    public async Task<LocalCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from local_credential where user_id = {userId}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LocalCredential>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpsertAsync(LocalCredential c, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into local_credential
                (id, user_id, password_hash, password_changed_at, must_change_password,
                 failed_attempt_count, locked_until, last_failed_at, created_at, updated_at)
            values
                ({c.Id}, {c.UserId}, {c.PasswordHash}, {c.PasswordChangedAt}, {c.MustChangePassword},
                 {c.FailedAttemptCount}, {c.LockedUntil}, {c.LastFailedAt}, {c.CreatedAt}, {c.UpdatedAt})
            on conflict (user_id) do update set
                password_hash = excluded.password_hash,
                password_changed_at = excluded.password_changed_at,
                must_change_password = excluded.must_change_password,
                failed_attempt_count = excluded.failed_attempt_count,
                locked_until = excluded.locked_until,
                last_failed_at = excluded.last_failed_at,
                updated_at = excluded.updated_at
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class MagicLinkRepository(IDbConnectionFactory factory) : IMagicLinkRepository
{
    public async Task InsertAsync(MagicLinkToken t, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into magic_link_token
                (id, user_id, token_hash, purpose, requested_at, expires_at, consumed_at, revoked_at,
                 request_ip, request_user_agent, created_at)
            values
                ({t.Id}, {t.UserId}, {t.TokenHash}, {t.Purpose}, {t.RequestedAt}, {t.ExpiresAt}, {t.ConsumedAt},
                 {t.RevokedAt}, {t.RequestIp}, {t.RequestUserAgent}, {t.CreatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<MagicLinkToken?> GetActiveByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            select * from magic_link_token
            where token_hash = {tokenHash} and consumed_at is null and revoked_at is null
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MagicLinkToken>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task MarkConsumedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"update magic_link_token set consumed_at = {when} where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset when, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update magic_link_token set revoked_at = {when}
            where user_id = {userId} and consumed_at is null and revoked_at is null
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> CountRecentForUserAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select count(*) from magic_link_token where user_id = {userId} and requested_at >= {since}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"delete from magic_link_token where expires_at < {olderThan}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class SessionRepository(IDbConnectionFactory factory) : ISessionRepository
{
    public async Task InsertAsync(UserSession s, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into user_session
                (id, user_id, session_token_hash, authentication_method, identity_provider_id,
                 created_at, expires_at, last_seen_at, revoked_at, ip_address, user_agent)
            values
                ({s.Id}, {s.UserId}, {s.SessionTokenHash}, {s.AuthenticationMethod}, {s.IdentityProviderId},
                 {s.CreatedAt}, {s.ExpiresAt}, {s.LastSeenAt}, {s.RevokedAt}, {s.IpAddress}, {s.UserAgent})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<UserSession?> GetActiveByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            select * from user_session
            where session_token_hash = {tokenHash} and revoked_at is null and expires_at > now()
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UserSession>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task TouchAsync(Guid id, DateTimeOffset when, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"update user_session set last_seen_at = {when} where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task RevokeAsync(Guid id, DateTimeOffset when, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"update user_session set revoked_at = {when} where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"delete from user_session where expires_at < {olderThan}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class IdentityProviderRepository(IDbConnectionFactory factory) : IIdentityProviderRepository
{
    public async Task<IdentityProvider?> GetEnabledByTypeAsync(string providerType, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from identity_provider where provider_type = {providerType} and enabled = true");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<IdentityProvider>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IdentityProvider?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from identity_provider where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<IdentityProvider>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpsertGoogleAsync(IdentityProvider p, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into identity_provider
                (id, provider_type, name, issuer, client_id, secret_reference, hosted_domain, enabled, created_at, updated_at)
            values
                ({p.Id}, {p.ProviderType}, {p.Name}, {p.Issuer}, {p.ClientId}, {p.SecretReference},
                 {p.HostedDomain}, {p.Enabled}, {p.CreatedAt}, {p.UpdatedAt})
            on conflict (provider_type) do update set
                name = excluded.name, issuer = excluded.issuer, client_id = excluded.client_id,
                secret_reference = excluded.secret_reference, hosted_domain = excluded.hosted_domain,
                enabled = excluded.enabled, updated_at = excluded.updated_at
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class SocialIdentityRepository(IDbConnectionFactory factory) : ISocialIdentityRepository
{
    public async Task<SocialIdentity?> GetByProviderSubjectAsync(Guid providerId, string subject, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            select * from social_identity where identity_provider_id = {providerId} and provider_subject = {subject}
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SocialIdentity>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<SocialIdentity?> GetByUserAndProviderAsync(Guid userId, Guid providerId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            select * from social_identity where user_id = {userId} and identity_provider_id = {providerId}
            """);
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SocialIdentity>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task InsertAsync(SocialIdentity s, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into social_identity
                (id, user_id, identity_provider_id, provider_subject, provider_email, normalized_provider_email,
                 provider_email_verified, hosted_domain, linked_at, last_login_at, created_at, updated_at)
            values
                ({s.Id}, {s.UserId}, {s.IdentityProviderId}, {s.ProviderSubject}, {s.ProviderEmail},
                 {s.NormalizedProviderEmail}, {s.ProviderEmailVerified}, {s.HostedDomain}, {s.LinkedAt},
                 {s.LastLoginAt}, {s.CreatedAt}, {s.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task UpdateLastLoginAsync(Guid id, DateTimeOffset when, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"update social_identity set last_login_at = {when}, updated_at = {when} where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}
