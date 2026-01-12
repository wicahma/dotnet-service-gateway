using K8sGateway.Core.Constants;

namespace K8sGateway.Host.Middleware;

// Middleware that ensures correlation IDs are present on all requests.
// Generates a new correlation ID if one is not provided.
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? correlationId = context.Request.Headers[HeaderNames.CorrelationId].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[HeaderNames.CorrelationId] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderNames.CorrelationId] = correlationId;
            return Task.CompletedTask;
        });

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

// Extension methods for CorrelationIdMiddleware.
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
