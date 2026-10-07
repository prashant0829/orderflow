namespace OrderFlow.Gateway.RateLimiting;

public record RateLimitDecision(bool Allowed, int Limit, int Remaining, TimeSpan RetryAfter);

public interface IRateLimiter
{
    Task<RateLimitDecision> CheckAsync(string key, RateLimitPolicy policy, CancellationToken cancellationToken);
}
