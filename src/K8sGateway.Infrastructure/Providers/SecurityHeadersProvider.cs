using K8sGateway.Core.Constants;
using K8sGateway.Core.Interfaces;

namespace K8sGateway.Infrastructure.Providers;

// Provides strict security headers for all gateway responses.
// Implements OWASP recommended security headers.
public sealed class SecurityHeadersProvider : ISecurityHeadersProvider
{
    private readonly Dictionary<string, string> _securityHeaders;

    public SecurityHeadersProvider()
    {
        _securityHeaders = new Dictionary<string, string>
        {
            [HeaderNames.StrictTransportSecurity] = "max-age=31536000; includeSubDomains; preload",
            [HeaderNames.XContentTypeOptions] = "nosniff",
            [HeaderNames.XFrameOptions] = "DENY",
            [HeaderNames.ContentSecurityPolicy] = "default-src 'self'; frame-ancestors 'none'",
            [HeaderNames.ReferrerPolicy] = "strict-origin-when-cross-origin",
            [HeaderNames.PermissionsPolicy] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()",
            [HeaderNames.XPermittedCrossDomainPolicies] = "none"
        };
    }

    public IDictionary<string, string> GetSecurityHeaders() => _securityHeaders;
}
