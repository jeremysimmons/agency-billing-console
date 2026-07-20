using System.Net.Http.Headers;
using Aib.Application;
using Aib.Application.Abstractions;
using Aib.Application.Integrations;
using Aib.Infrastructure.Auth;
using Aib.Infrastructure.Email;
using Aib.Infrastructure.Integrations;
using Aib.Infrastructure.Persistence;
using Aib.Infrastructure.Persistence.Repositories;
using Aib.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.AddOptions<MailOptions>()
            .Bind(configuration.GetSection(MailOptions.SectionName))
            .PostConfigure(o =>
            {
                // Laravel-style MAIL_* env vars (used by local .env / docker).
                o.Host = EnvOr(o.Host, "MAIL_HOST") ?? o.Host;
                if (int.TryParse(Environment.GetEnvironmentVariable("MAIL_PORT"), out var port))
                    o.Port = port;
                o.Encryption = NullIfLiteralNull(EnvOr(o.Encryption, "MAIL_ENCRYPTION"));
                o.Username = NullIfLiteralNull(EnvOr(o.Username, "MAIL_USERNAME"));
                o.Password = NullIfLiteralNull(EnvOr(o.Password, "MAIL_PASSWORD"));
                o.From = EnvOr(o.From, "MAIL_FROM") ?? o.From;
            });

        // Security / integrations
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        RegisterEmailSender(services, configuration);
        AddClickUp(services, configuration);

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ILocalCredentialRepository, LocalCredentialRepository>();
        services.AddScoped<IMagicLinkRepository, MagicLinkRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IIdentityProviderRepository, IdentityProviderRepository>();
        services.AddScoped<ISocialIdentityRepository, SocialIdentityRepository>();
        services.AddScoped<IAgencyRepository, AgencyRepository>();
        services.AddScoped<IContractorRepository, ContractorRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        return services;
    }

    private static void AddClickUp(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClickUpOptions>()
            .Bind(configuration.GetSection(ClickUpOptions.SectionName))
            .PostConfigure(o =>
            {
                o.ApiToken ??= Environment.GetEnvironmentVariable("CLICKUP_API_TOKEN");
                o.TeamId ??= Environment.GetEnvironmentVariable("CLICKUP_TEAM_ID");
            });

        services.AddHttpClient<IClickUpHierarchyBuilder, ClickUpHierarchyBuilder>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClickUpOptions>>().Value;
            http.BaseAddress = new Uri(opts.ApiBaseUrl.EndsWith('/') ? opts.ApiBaseUrl : opts.ApiBaseUrl + "/");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", opts.ApiToken);
        });
    }

    private static void RegisterEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var mail = new MailOptions();
        configuration.GetSection(MailOptions.SectionName).Bind(mail);
        mail.Host = Environment.GetEnvironmentVariable("MAIL_HOST") ?? mail.Host;

        // Prefer SMTP when a host is configured (Mailpit in local dev); otherwise log to console.
        if (!string.IsNullOrWhiteSpace(mail.Host))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
    }

    private static string? EnvOr(string? current, string envName)
        => Environment.GetEnvironmentVariable(envName) ?? current;

    private static string? NullIfLiteralNull(string? value)
        => string.IsNullOrWhiteSpace(value)
           || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
}
