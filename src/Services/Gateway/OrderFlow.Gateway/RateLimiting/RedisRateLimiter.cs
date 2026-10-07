using StackExchange.Redis;

namespace OrderFlow.Gateway.RateLimiting;

/// <summary>
/// Fixed-window counter in Redis, shared by every gateway instance. One Lua script does "count + set expiry" atomically,
/// so a crash between the two steps can never leave a counter that never expires. If Redis is down we let requests through.
/// </summary>
public class RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> logger) : IRateLimiter
{
    private const string CountScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        local ttl = redis.call('PTTL', KEYS[1])
        return {current, ttl}
        """;

    public async Task<RateLimitDecision> CheckAsync(string key, RateLimitPolicy policy, CancellationToken cancellationToken)
    {
        try
        {
            var windowMs = policy.WindowSeconds * 1000;
            var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(CountScript, [key], [windowMs]))!;
            var count = (long)result[0];
            var ttlMs = Math.Max((long)result[1], 0);

            var remaining = (int)Math.Max(policy.Limit - count, 0);
            return new RateLimitDecision(count <= policy.Limit, policy.Limit, remaining, TimeSpan.FromMilliseconds(ttlMs));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            logger.LogWarning(exception, "Rate limiter could not reach Redis; allowing the request");
            return new RateLimitDecision(true, policy.Limit, policy.Limit, TimeSpan.Zero);
        }
    }
}
