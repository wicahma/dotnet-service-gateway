using System.Net;
using System.Text.Json;
using K8sGateway.Core.Constants;
using Microsoft.AspNetCore.Mvc;

namespace K8sGateway.Host.Middleware;

// Global exception handler that returns RFC 7807 Problem Details.
// Ensures the proxy never crashes and always returns meaningful error responses.
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            if (context.Response.StatusCode >= 500 && !context.Response.HasStarted)
            {
                var correlationId = context.Items[HeaderNames.CorrelationId]?.ToString();
                _logger.LogError(
                    "Downstream service returned {StatusCode} for {Method} {Path}. CorrelationId: {CorrelationId}",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[HeaderNames.CorrelationId]?.ToString() ?? "unknown";

        _logger.LogError(
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
            correlationId,
            context.Request.Path,
            context.Request.Method);

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response has already started, cannot write error response");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.BadGateway,
            Title = "Gateway Error",
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "An error occurred while processing your request.",
            Instance = context.Request.Path,
            Type = "https://httpstatuses.com/502"
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}

// Extension methods for GlobalExceptionHandlerMiddleware.
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
