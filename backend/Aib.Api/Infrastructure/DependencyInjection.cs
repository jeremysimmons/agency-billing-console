using Aib.Application;
using Aib.Application.Abstractions;
using Aib.Application.Integrations;
using Aib.Infrastructure.Integrations;
using Aib.Infrastructure.Persistence;
using Aib.Infrastructure.Persistence.Repositories;
using Aib.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Aib.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperConfig.Configure();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddSingleton<IClock, SystemClock>();
        services.AddOptions<InvoiceOptions>()
            .Bind(configuration.GetSection(InvoiceOptions.SectionName));
        AddClickUp(services, configuration);

        services.AddScoped<IAgencyRepository, AgencyRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IClickUpContainerRepository, ClickUpContainerRepository>();
        services.AddScoped<IClickUpSyncRunRepository, ClickUpSyncRunRepository>();

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
                o.AssigneeId ??= Environment.GetEnvironmentVariable("CLICKUP_ASSIGNEE_ID");
            });

        void ConfigureClient(IServiceProvider sp, HttpClient http)
        {
            var opts = sp.GetRequiredService<IOptions<ClickUpOptions>>().Value;
            http.BaseAddress = new Uri(opts.ApiBaseUrl.EndsWith('/') ? opts.ApiBaseUrl : opts.ApiBaseUrl + "/");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(opts.ApiToken))
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", opts.ApiToken);
        }

        services.AddHttpClient<IClickUpHierarchyBuilder, ClickUpHierarchyBuilder>(ConfigureClient);
        services.AddHttpClient<IClickUpClient, ClickUpClient>(ConfigureClient);
    }
}
