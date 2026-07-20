using Aib.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aib.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<AccessService>();
        services.AddScoped<AgencyService>();
        services.AddScoped<ClientService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<TaskService>();
        services.AddScoped<RollupService>();
        services.AddScoped<WorkReviewService>();
        return services;
    }
}
