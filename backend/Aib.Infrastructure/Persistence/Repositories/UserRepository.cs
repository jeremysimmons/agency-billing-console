using Aib.Application.Abstractions;
using Aib.Domain.Entities;
using Dapper;
using Dapper.SimpleSqlBuilder;

namespace Aib.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IDbConnectionFactory factory) : IUserRepository
{
    public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from app_user where id = {id}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from app_user where normalized_username = {normalizedUsername}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select * from app_user where normalized_email = {normalizedEmail}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AppUser>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<Guid> InsertAsync(AppUser u, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into app_user
                (id, agency_id, contractor_id, username, normalized_username, email, normalized_email,
                 display_name, status, email_verified_at, password_login_enabled, magic_link_enabled,
                 social_login_enabled, last_login_at, created_at, updated_at)
            values
                ({u.Id}, {u.AgencyId}, {u.ContractorId}, {u.Username}, {u.NormalizedUsername}, {u.Email},
                 {u.NormalizedEmail}, {u.DisplayName}, {u.Status}, {u.EmailVerifiedAt}, {u.PasswordLoginEnabled},
                 {u.MagicLinkEnabled}, {u.SocialLoginEnabled}, {u.LastLoginAt}, {u.CreatedAt}, {u.UpdatedAt})
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return u.Id;
    }

    public async Task UpdateAsync(AppUser u, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            update app_user set
                agency_id = {u.AgencyId}, contractor_id = {u.ContractorId}, username = {u.Username},
                normalized_username = {u.NormalizedUsername}, email = {u.Email}, normalized_email = {u.NormalizedEmail},
                display_name = {u.DisplayName}, status = {u.Status}, email_verified_at = {u.EmailVerifiedAt},
                password_login_enabled = {u.PasswordLoginEnabled}, magic_link_enabled = {u.MagicLinkEnabled},
                social_login_enabled = {u.SocialLoginEnabled}, last_login_at = {u.LastLoginAt}, updated_at = {u.UpdatedAt}
            where id = {u.Id}
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            select r.name from role r
            join user_role ur on ur.role_id = r.id
            where ur.user_id = {userId}
            """);
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AddRoleAsync(Guid userId, int roleId, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"""
            insert into user_role (user_id, role_id) values ({userId}, {roleId})
            on conflict do nothing
            """);
        using var conn = await factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }
}

public sealed class RoleRepository(IDbConnectionFactory factory) : IRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<Role>(new CommandDefinition("select id, name from role", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var builder = SimpleBuilder.Create($"select id, name from role where name = {name}");
        using var conn = await factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Role>(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
    }

    public async Task EnsureSeededAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        using var conn = await factory.OpenAsync(ct);
        foreach (var name in names)
        {
            var builder = SimpleBuilder.Create($"insert into role (name) values ({name}) on conflict (name) do nothing");
            await conn.ExecuteAsync(new CommandDefinition(builder.Sql, builder.Parameters, cancellationToken: ct));
        }
    }
}
