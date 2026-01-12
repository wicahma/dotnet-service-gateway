# K8s Gateway - Production-Ready API Gateway

## Overview

K8s Gateway is a high-performance, Kubernetes-native API Gateway built with .NET 9 and YARP (Yet Another Reverse Proxy). It's designed to serve as the backbone of microservices architectures with near-zero latency overhead, comprehensive security, and enterprise-grade observability.

## Architecture

This solution follows **Clean Architecture** principles with clear separation of concerns:

```
K8sGateway/
├── src/
│   ├── K8sGateway.Core/              # Domain abstractions and constants
│   │   ├── Interfaces/               # ICorrelationIdProvider, ISecurityHeadersProvider
│   │   └── Constants/                # HeaderNames, RateLimitPolicies
│   │
│   ├── K8sGateway.Infrastructure/    # Implementation of core abstractions
│   │   ├── Providers/                # CorrelationIdProvider, SecurityHeadersProvider
│   │   └── DependencyInjection.cs
│   │
│   └── K8sGateway.Host/              # ASP.NET Core host (API Gateway)
│       ├── Middleware/               # SecurityHeaders, CorrelationId, GlobalExceptionHandler
│       ├── Program.cs                # YARP configuration and middleware pipeline
│       └── appsettings.json          # Route and cluster configuration
│
├── k8s/                              # Kubernetes manifests
└── Dockerfile                        # Multi-stage container build
```

## Key Features

### 1. **YARP-Powered Routing**
- Configuration-driven routing (no code recompilation needed)
- Hot-reloadable routes and clusters from `appsettings.json`
- Support for HTTP/1.1, HTTP/2, HTTP/3 (QUIC)
- Built-in gRPC and WebSocket support

### 2. **Kubernetes-Native**
- Service discovery via DNS (e.g., `http://service-name.namespace.svc.cluster.local`)
- Health checks for liveness (`/health/live`) and readiness (`/health/ready`)
- Graceful shutdown support
- Resource-aware (respects K8s CPU/memory limits)

### 3. **Resilience & Rate Limiting**
- **Token Bucket** algorithm for global rate limiting
- **Fixed Window**, **Sliding Window** policies for per-route limiting
- Concurrency limiting
- Automatic rejection with RFC 7807 Problem Details

### 4. **Security**
- Strict security headers (HSTS, CSP, X-Frame-Options, etc.)
- Correlation ID tracking for request tracing
- Global exception handler (prevents crashes)
- Non-root container execution
- Request/response logging with structured logging (Serilog)

### 5. **Observability**
- Structured logging with Serilog
- Correlation IDs for distributed tracing
- Request logging with timing and status codes
- Health check endpoints for monitoring

### 6. **Performance Optimization**
- Server Garbage Collection enabled
- Tiered JIT compilation
- Connection pooling (MaxConnectionsPerServer: 1000)
- Kestrel tuning for high throughput
- No artificial concurrency limits

## Configuration

### appsettings.json Structure

```json
{
  "ReverseProxy": {
    "Routes": {
      "route_name": {
        "ClusterId": "cluster_name",
        "RateLimiterPolicy": "FixedWindowPolicy",
        "Match": { "Path": "/api/service/{**remainder}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/service" },
          { "RequestHeader": "X-Forwarded-Prefix", "Set": "/api/service" }
        ]
      }
    },
    "Clusters": {
      "cluster_name": {
        "Destinations": {
          "dest1": {
            "Address": "http://service-name.namespace.svc.cluster.local:80"
          }
        },
        "HttpClient": {
          "MaxConnectionsPerServer": 1000
        }
      }
    }
  }
}
```

### Adding a New Microservice

**No code changes required!** Simply update `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "my_new_service": {
        "ClusterId": "my_service_cluster",
        "RateLimiterPolicy": "TokenBucketPolicy",
        "Match": { "Path": "/api/myservice/{**remainder}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/myservice" }
        ]
      }
    },
    "Clusters": {
      "my_service_cluster": {
        "Destinations": {
          "dest1": {
            "Address": "http://my-service.default.svc.cluster.local:80"
          }
        }
      }
    }
  }
}
```

## Building & Running

### Local Development

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run the gateway (listens on http://localhost:5000)
dotnet run --project src/K8sGateway.Host
```

Visit `http://localhost:5000/health/live` to verify the gateway is running.

### Docker Build

```bash
# Build Docker image
docker build -t k8sgateway:latest .

# Run container
docker run -p 80:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  k8sgateway:latest
```

### Kubernetes Deployment

Create a ConfigMap for your routes (example in `k8s/configmap.yaml`):

```bash
kubectl apply -f k8s/
```

## Middleware Pipeline

The request flows through the following middleware in order:

1. **GlobalExceptionHandlerMiddleware** - Catches all exceptions, returns RFC 7807 Problem Details
2. **CorrelationIdMiddleware** - Ensures all requests have correlation IDs for tracing
3. **SerilogRequestLogging** - Structured request/response logging
4. **SecurityHeadersMiddleware** - Adds strict security headers to all responses
5. **RateLimiter** - Enforces rate limiting policies
6. **MapReverseProxy** - YARP routing to destination services

## Rate Limiting Policies

### Global Rate Limit (Token Bucket)
- **Tokens:** 100 per client IP
- **Replenishment:** 20 tokens every 10 seconds
- **Queue:** 10 requests queued per client

### FixedWindowPolicy
- **Limit:** 100 requests per minute
- **Queue:** 10 requests

### SlidingWindowPolicy
- **Limit:** 100 requests per minute (across 6 segments)
- **Queue:** 10 requests

### TokenBucketPolicy
- **Tokens:** 100 per route
- **Replenishment:** 20 tokens every 10 seconds
- **Queue:** 10 requests

### ConcurrencyPolicy
- **Limit:** 50 concurrent requests
- **Queue:** 25 requests

Rejected requests return `HTTP 429 Too Many Requests` with a `Retry-After` header.

## Security Headers

All responses include:

| Header | Value | Purpose |
|--------|-------|---------|
| `Strict-Transport-Security` | max-age=31536000; includeSubDomains; preload | Enforce HTTPS |
| `X-Content-Type-Options` | nosniff | Prevent MIME sniffing |
| `X-Frame-Options` | DENY | Prevent clickjacking |
| `Content-Security-Policy` | default-src 'self'; frame-ancestors 'none' | Strict CSP |
| `Referrer-Policy` | strict-origin-when-cross-origin | Control referrer |
| `Permissions-Policy` | (cameras, microphone, payment, etc. disabled) | Restrict browser features |
| `X-Permitted-Cross-Domain-Policies` | none | Block Flash/PDF embedding |

## Error Handling

The gateway never crashes. All errors return `HTTP 502 Bad Gateway` with RFC 7807 Problem Details:

```json
{
  "type": "https://httpstatuses.com/502",
  "title": "Gateway Error",
  "status": 502,
  "detail": "An error occurred while processing your request.",
  "instance": "/api/users/123",
  "correlationId": "a1b2c3d4e5f6g7h8",
  "traceId": "0hm15r5gjj5n3s8p:00000001"
}
```

Every error is logged with its correlation ID for troubleshooting.

## Monitoring & Health Checks

### Kubernetes Liveness Probe

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 80
  initialDelaySeconds: 5
  periodSeconds: 30
```

### Kubernetes Readiness Probe

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 80
  initialDelaySeconds: 5
  periodSeconds: 10
```

### Logs

View structured logs with correlation ID:

```bash
# All logs for a specific correlation ID
kubectl logs <pod> | grep "a1b2c3d4e5f6g7h8"

# Following logs
kubectl logs -f <pod>
```

## Performance Tuning

### Kestrel Configuration

The gateway is configured for maximum throughput:

```csharp
serverOptions.Limits.MaxConcurrentConnections = null;  // No limit
serverOptions.Limits.MaxRequestBodySize = 100MB;
serverOptions.Limits.MinRequestBodyDataRate = null;    // Allow WebSockets
serverOptions.Limits.KeepAliveTimeout = 2 minutes;
serverOptions.ConfigureEndpointDefaults(listenOptions =>
    listenOptions.Protocols = Http1AndHttp2AndHttp3);
```

### Connection Pooling

Each cluster maintains a pool of up to 1,000 connections to destination services, reducing SNAT port exhaustion.

### Garbage Collection

- **Server GC** enabled for better throughput
- **Tiered JIT** enabled for faster startup and better long-term performance

## Security Best Practices

⚠️ **Important:**

1. **WAF Protection:** For public-facing deployments, place a WAF (Cloudflare, Azure Front Door) in front of the gateway.
2. **Rate Limiting:** The default policy is aggressive but configurable. Adjust based on your upstream service capacity.
3. **HTTPS:** Always use TLS in production. Modify `appsettings.json` to use HTTPS endpoints.
4. **RBAC:** The gateway runs as non-root user in containers.
5. **Network Policies:** Use Kubernetes NetworkPolicies to restrict traffic to/from the gateway.

## Troubleshooting

### Gateway returning 502 for all requests

1. Check gateway logs: `kubectl logs <gateway-pod>`
2. Verify destination service DNS: `kubectl exec <gateway-pod> -- nslookup service-name.namespace.svc.cluster.local`
3. Check rate limiting: Is the client hitting the rate limit? (HTTP 429)

### High latency

1. Monitor connection pooling: `kubectl top pod`
2. Check downstream service response times
3. Increase `MaxConnectionsPerServer` if SNAT exhaustion is detected

### Memory usage increasing

1. Monitor garbage collection: Add GC logging in appsettings
2. Check for memory leaks in rate limiter queue
3. Verify large request bodies aren't being buffered

## Contributing

This project uses strict warning-as-errors compilation. All code must:

- Follow C# naming conventions
- Use nullable reference types (`#nullable enable`)
- Include XML documentation on public types
- Pass code analysis

## License

[Your License Here]

## Support

For issues or questions, please open an issue or contact the team.
