using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.Application.Stock.Dtos;

public record StockItemDto(Guid ProductId, string Name, decimal UnitPrice, int AvailableQuantity, DateTime UpdatedAtUtc);

public static class StockItemMapping
{
    public static StockItemDto ToDto(this StockItem item) =>
        new(item.Id, item.Name, item.UnitPrice, item.AvailableQuantity, item.UpdatedAtUtc);
}
