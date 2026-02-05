using System.Diagnostics;
using System.Text;
using K8sGateway.Core.Constants;
using Serilog;

namespace K8sGateway.Host.Middleware;

public sealed class RequestResponseLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestResponseLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger = logger;
    private const int MaxBodyLogSize = 4096; // 4KB max for body logging
    private const string EmptyBodyPlaceholder = "(empty)";
    private const string UnknownPlaceholder = "unknown";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        Stopwatch? stopwatch = Stopwatch.StartNew();
        string? correlationId = context.Items[HeaderNames.CorrelationId]?.ToString() ?? UnknownPlaceholder;

        context.Request.EnableBuffering();

        try
        {
            await _next(context);

            stopwatch.Stop();

            bool isError = context.Response.StatusCode >= 400;

            if (isError)
            {
                string? requestBody = await ReadRequestBodyAsync(context.Request);

                _logger.LogError(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | CorrelationId: {CorrelationId} | RemoteIP: {RemoteIP} | RequestBody: {RequestBody}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId,
                    context.Connection.RemoteIpAddress?.ToString() ?? UnknownPlaceholder,
                    requestBody ?? EmptyBodyPlaceholder);
            }
            else
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            string? requestBody = await ReadRequestBodyAsync(context.Request);

            _logger.LogError(ex,
                "HTTP {Method} {Path} threw exception after {ElapsedMs}ms | CorrelationId: {CorrelationId} | RemoteIP: {RemoteIP} | RequestBody: {RequestBody}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                correlationId,
                context.Connection.RemoteIpAddress?.ToString() ?? UnknownPlaceholder,
                requestBody ?? EmptyBodyPlaceholder);

            throw;
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            if (!request.Body.CanSeek)
            {
                return "(body not readable)";
            }

            if (request.ContentLength > MaxBodyLogSize)
            {
                return $"(body too large: {request.ContentLength} bytes)";
            }

            request.Body.Seek(0, SeekOrigin.Begin);

            using StreamReader? reader = new(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            string? body = await reader.ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);

            return string.IsNullOrWhiteSpace(body) ? EmptyBodyPlaceholder : body;
        }
        catch (Exception)
        {
            return "(error reading body)";
        }
    }
}

public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}
