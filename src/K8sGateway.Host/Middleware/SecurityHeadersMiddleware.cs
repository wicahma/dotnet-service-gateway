using K8sGateway.Core.Interfaces;

namespace K8sGateway.Host.Middleware;

// Middleware that adds security headers to all responses.
// Must be placed early in the pipeline to ensure all responses are secured.
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDictionary<string, string> _securityHeaders;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ISecurityHeadersProvider securityHeadersProvider)
    {
        _next = next;
        _securityHeaders = securityHeadersProvider.GetSecurityHeaders();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            foreach (var header in _securityHeaders)
            {
                if (!context.Response.Headers.ContainsKey(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

// Extension methods for SecurityHeadersMiddleware.
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
