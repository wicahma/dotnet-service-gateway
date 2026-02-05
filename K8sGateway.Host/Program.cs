using System.Threading.RateLimiting;
using K8sGateway.Core.Constants;
using K8sGateway.Host.Middleware;
using K8sGateway.Infrastructure;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting K8s Gateway");

    WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

    Log.Information("Configuring Serilog");
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    Log.Information("Adding Infrastructure services");
    builder.Services.AddInfrastructure();

    Log.Information("Configuring Reverse Proxy");
    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    Log.Information("Configuring Rate Limiter");
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 100,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    TokensPerPeriod = 20,
                    AutoReplenishment = true
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            string? correlationId = context.HttpContext.Items[HeaderNames.CorrelationId]?.ToString() ?? "unknown";

            Log.Warning(
                "Rate limit exceeded for {RemoteIp}. CorrelationId: {CorrelationId}, Path: {Path}",
                context.HttpContext.Connection.RemoteIpAddress,
                correlationId,
                context.HttpContext.Request.Path);

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/problem+json";

            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                ? retry.TotalSeconds
                : 60;

            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/429",
                title = "Too Many Requests",
                status = 429,
                detail = $"Rate limit exceeded. Please retry after {retryAfter} seconds.",
                instance = context.HttpContext.Request.Path.ToString(),
                correlationId,
                retryAfter
            }, cancellationToken);
        };
    });

    Log.Information("Adding Health Checks");
    builder.Services.AddHealthChecks();

    Log.Information("Configuring Kestrel");
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        // Remove connection limits - let K8s handle resource limits
        serverOptions.Limits.MaxConcurrentConnections = null;
        serverOptions.Limits.MaxConcurrentUpgradedConnections = null;

        // Disable request body rate limiting for WebSocket/streaming support
        serverOptions.Limits.MinRequestBodyDataRate = null;
        serverOptions.Limits.MinResponseDataRate = null;

        // Increase request body size for large payloads
        serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB

        // Keep-alive settings for connection pooling
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

        // Enable HTTP/2 (HTTP/3 can cause issues on some platforms)
        serverOptions.ConfigureEndpointDefaults(listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    });

    Log.Information("Building application");
    WebApplication? app = builder.Build();

    Log.Information("Configuring middleware pipeline");
    app.UseGlobalExceptionHandler();
    app.UseCorrelationId();
    app.UseRequestResponseLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseSecurityHeaders();
    app.UseRateLimiter();
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true
    });
    app.MapReverseProxy();

    Log.Information("Starting web server");
    await app.StartAsync();
    Log.Information("K8s Gateway is ready to accept connections");
    await app.WaitForShutdownAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "K8s Gateway terminated unexpectedly");
    return;
}
finally
{
    await Log.CloseAndFlushAsync();
}
