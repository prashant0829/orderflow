using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Caching;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddOrderFlowRedisCache(this IServiceCollection services, string? redisConnectionString, string instanceName)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = instanceName;
        });
        services.AddSingleton<ICacheService, RedisCacheService>();
        return services;
    }
}
