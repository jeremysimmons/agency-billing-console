using Aib.Application.Abstractions;
using Aib.Infrastructure.Auth;
using Aib.Infrastructure.Email;
using Aib.Infrastructure.Persistence;
using Aib.Infrastructure.Persistence.Repositories;
using Aib.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aib.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperConfig.Configure();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddSingleton(sp => new DatabaseMigrator(connectionString,
            sp.GetRequiredService<ILogger<DatabaseMigrator>>()));

        services.Configure<GoogleAuthOptions>(configuration.GetSection("Google"));

        // Security / integrations
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<IEmailSender, ConsoleEmailSender>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ILocalCredentialRepository, LocalCredentialRepository>();
        services.AddScoped<IMagicLinkRepository, MagicLinkRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IIdentityProviderRepository, IdentityProviderRepository>();
        services.AddScoped<ISocialIdentityRepository, SocialIdentityRepository>();
        services.AddScoped<IAuthEventRepository, AuthEventRepository>();
        services.AddScoped<IAgencyRepository, AgencyRepository>();
        services.AddScoped<IContractorRepository, ContractorRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientAccessRepository, ClientAccessRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        return services;
    }
}
