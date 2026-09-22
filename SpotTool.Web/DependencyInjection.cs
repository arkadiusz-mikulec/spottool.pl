using SpotTool.Web.Features.Shared.Services;

namespace SpotTool.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebDependencies(this IServiceCollection services)
    {
        services.AddScoped<SpotService>();

        return services;
    }
}