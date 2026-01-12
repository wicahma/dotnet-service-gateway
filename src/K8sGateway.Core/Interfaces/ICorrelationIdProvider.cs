namespace K8sGateway.Core.Interfaces;

// Abstraction for correlation ID generation and management.
// Used for request tracing across the gateway and downstream services.
public interface ICorrelationIdProvider
{
    // Gets the current correlation ID from the request context.
    string? GetCorrelationId();

    // Generates a new correlation ID if one doesn't exist.
    string GenerateCorrelationId();
}
