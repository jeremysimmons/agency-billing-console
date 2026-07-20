using System.Net.Http.Headers;
using Aib.Application;
using Aib.Application.Abstractions;
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
using Quartz;

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
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<ITimeEntrySourceRepository, TimeEntrySourceRepository>();
        services.AddScoped<IExternalTimeEntryQueryRepository, ExternalTimeEntryQueryRepository>();

        // ClickUp integration repositories
        services.AddScoped<IExternalConnectionRepository, ExternalConnectionRepository>();
        services.AddScoped<IExternalIdentityRepository, ExternalIdentityRepository>();
        services.AddScoped<IExternalContainerRepository, ExternalContainerRepository>();
        services.AddScoped<IExternalWorkItemRepository, ExternalWorkItemRepository>();
        services.AddScoped<IExternalTimeEntryRepository, ExternalTimeEntryRepository>();
        services.AddScoped<IImportRunRepository, ImportRunRepository>();
        services.AddScoped<IImportRecordRepository, ImportRecordRepository>();
        services.AddScoped<ISyncCursorRepository, SyncCursorRepository>();

        // Mapping repositories
        services.AddScoped<IExternalContainerMappingRepository, ExternalContainerMappingRepository>();
        services.AddScoped<IExternalTaskMappingRepository, ExternalTaskMappingRepository>();
        services.AddScoped<IExternalStatusMappingRepository, ExternalStatusMappingRepository>();
        services.AddScoped<IMappingQueryRepository, MappingQueryRepository>();

        AddClickUp(services, configuration);

        return services;
    }

    private static void AddClickUp(IServiceCollection services, IConfiguration configuration)
    {
        // Bind config; allow the secret token to arrive via env var (never persisted in the DB).
        services.AddOptions<ClickUpOptions>()
            .Bind(configuration.GetSection("ClickUp"))
            .PostConfigure(o =>
            {
                o.ApiToken ??= Environment.GetEnvironmentVariable("CLICKUP_API_TOKEN");
                o.TeamId ??= Environment.GetEnvironmentVariable("CLICKUP_TEAM_ID");
                o.AssigneeId ??= Environment.GetEnvironmentVariable("CLICKUP_ASSIGNEE_ID");
            });

        services.AddHttpClient<IClickUpClient, ClickUpClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClickUpOptions>>().Value;
            http.BaseAddress = new Uri(opts.ApiBaseUrl.EndsWith('/') ? opts.ApiBaseUrl : opts.ApiBaseUrl + "/");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", opts.ApiToken);
        });

        var clickUp = new ClickUpOptions();
        configuration.GetSection("ClickUp").Bind(clickUp);
        var scheduleEnabled = clickUp.ScheduleEnabled
            && (clickUp.ApiToken ?? Environment.GetEnvironmentVariable("CLICKUP_API_TOKEN")) is not null
            && (clickUp.TeamId ?? Environment.GetEnvironmentVariable("CLICKUP_TEAM_ID")) is not null;

        services.AddQuartz(q =>
        {
            if (!scheduleEnabled) return;
            q.AddJob<ClickUpImportJob>(j => j.WithIdentity(ClickUpImportJob.Key));
            q.AddTrigger(t => t
                .ForJob(ClickUpImportJob.Key)
                .WithIdentity("clickup-incremental-trigger")
                .WithCronSchedule(clickUp.ImportCron));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
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
