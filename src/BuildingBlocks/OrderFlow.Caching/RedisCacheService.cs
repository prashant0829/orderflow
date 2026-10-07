using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Caching;

public class RedisCacheService(IDistributedCache distributedCache, ILogger<RedisCacheService> logger) : ICacheService
{
    public const string ActivitySourceName = "OrderFlow.Cache";
    private static readonly ActivitySource Source = new(ActivitySourceName);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity("cache.get");
        activity?.SetTag("cache.key", key);

        try
        {
            var bytes = await distributedCache.GetAsync(key, cancellationToken);
            activity?.SetTag("cache.hit", bytes is not null);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache read failed for {CacheKey}; falling back to database", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive };
            await distributedCache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), options, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache write failed for {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cache removal failed for {CacheKey}", key);
        }
    }
}
