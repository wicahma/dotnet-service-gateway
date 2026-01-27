namespace K8sGateway.Core.Constants;

// Standard HTTP header names used throughout the gateway.
public static class HeaderNames
{
    // Correlation & Tracing
    public const string CorrelationId = "X-Correlation-ID";
    public const string RequestId = "X-Request-ID";

    // Forwarding Headers
    public const string ForwardedFor = "X-Forwarded-For";
    public const string ForwardedHost = "X-Forwarded-Host";
    public const string ForwardedProto = "X-Forwarded-Proto";
    public const string ForwardedPrefix = "X-Forwarded-Prefix";

    // Security Headers
    public const string StrictTransportSecurity = "Strict-Transport-Security";
    public const string XContentTypeOptions = "X-Content-Type-Options";
    public const string XFrameOptions = "X-Frame-Options";
    public const string ContentSecurityPolicy = "Content-Security-Policy";
    public const string ReferrerPolicy = "Referrer-Policy";
    public const string PermissionsPolicy = "Permissions-Policy";
    public const string XPermittedCrossDomainPolicies = "X-Permitted-Cross-Domain-Policies";
}
