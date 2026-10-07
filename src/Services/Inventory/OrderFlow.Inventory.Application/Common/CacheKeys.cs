namespace OrderFlow.Inventory.Application.Common;

public static class CacheKeys
{
    public const string StockList = "inventory:stock:list";

    public static string StockItem(Guid productId) => $"inventory:stock:item:{productId}";
}
