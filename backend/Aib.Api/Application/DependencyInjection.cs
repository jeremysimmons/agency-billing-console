using Aib.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aib.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AgencyService>();
        services.AddScoped<ClientService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<ClickUpSyncService>();
        services.AddScoped<CsvTaskImportService>();
        return services;
    }
}
