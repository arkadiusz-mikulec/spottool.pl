using FastEndpoints;
using Marten;

namespace SpotTool.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebDependencies(this IServiceCollection services)
    {
        //services.AddScoped<Feature.Spots.CreateFromTms.CreateEndpoint>();

        return services;
    }
}