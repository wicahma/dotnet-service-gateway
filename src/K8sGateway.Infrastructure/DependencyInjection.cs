using K8sGateway.Core.Interfaces;
using K8sGateway.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace K8sGateway.Infrastructure;

// Extension methods for registering Infrastructure services.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddSingleton<ISecurityHeadersProvider, SecurityHeadersProvider>();

        return services;
    }
}
