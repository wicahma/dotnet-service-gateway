namespace K8sGateway.Core.Interfaces;

// Abstraction for security header configuration.
public interface ISecurityHeadersProvider
{
    // Gets the security headers to be applied to all responses.
    IDictionary<string, string> GetSecurityHeaders();
}
