using K8sGateway.Core.Interfaces;

namespace K8sGateway.Host.Middleware;

// Middleware that adds security headers to all responses.
// Must be placed early in the pipeline to ensure all responses are secured.
public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    ISecurityHeadersProvider securityHeadersProvider)
{
    private readonly RequestDelegate _next = next;
    private readonly IDictionary<string, string> _securityHeaders = securityHeadersProvider.GetSecurityHeaders();

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            bool isSwaggerEndpoint = context.Request.Path.StartsWithSegments("/api-docs") ||
                                   context.Request.Path.StartsWithSegments("/swagger");

            foreach (KeyValuePair<string, string> header in _securityHeaders.Where(h => !context.Response.Headers.ContainsKey(h.Key)))
            {
                if (isSwaggerEndpoint && header.Key == "Content-Security-Policy")
                {
                    context.Response.Headers[header.Key] =
                        "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'";
                }
                else
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
