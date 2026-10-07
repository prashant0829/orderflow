using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.Infrastructure.Persistence;

/// <summary>Demo catalog with fixed ids so scripts and the UI can refer to the same products.</summary>
public static class InventorySeeder
{
    public static async Task SeedAsync(InventoryDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.StockItems.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.StockItems.AddRange(
            StockItem.Create(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "Mechanical Keyboard", 89.99m, 50),
            StockItem.Create(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), "Wireless Mouse", 39.50m, 100),
            StockItem.Create(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), "27in 4K Monitor", 329.00m, 20),
            StockItem.Create(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"), "USB-C Dock", 119.00m, 30),
            StockItem.Create(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"), "Limited Edition Desk Mat", 49.00m, 3));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
