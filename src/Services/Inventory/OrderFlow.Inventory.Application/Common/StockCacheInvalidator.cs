using OrderFlow.Caching;

namespace OrderFlow.Inventory.Application.Common;

public static class StockCacheInvalidator
{
    public static async Task InvalidateAsync(this ICacheService cache, IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.StockList, cancellationToken);
        foreach (var productId in productIds)
        {
            await cache.RemoveAsync(CacheKeys.StockItem(productId), cancellationToken);
        }
    }
}
