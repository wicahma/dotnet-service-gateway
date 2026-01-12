namespace K8sGateway.Core.Constants;

// Rate limiting policy names used throughout the gateway.
public static class RateLimitPolicies
{
    public const string FixedWindow = "FixedWindowPolicy";
    public const string SlidingWindow = "SlidingWindowPolicy";
    public const string TokenBucket = "TokenBucketPolicy";
    public const string Concurrency = "ConcurrencyPolicy";
}
