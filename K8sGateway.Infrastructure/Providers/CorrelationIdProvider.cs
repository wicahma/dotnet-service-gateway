using K8sGateway.Core.Interfaces;

namespace K8sGateway.Infrastructure.Providers;

// Provides correlation ID generation using GUID.
// Thread-safe implementation for high-throughput scenarios.
public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AsyncLocal<string?> _correlationId = new();

    public string? GetCorrelationId() => _correlationId.Value;

    public string GenerateCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _correlationId.Value = correlationId;
        return correlationId;
    }
}
