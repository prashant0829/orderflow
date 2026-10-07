using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Domain.Reservations;
using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.Infrastructure.Persistence;

public class StockRepository(InventoryDbContext dbContext) : IStockRepository
{
    // NOLOCK: a read that never waits for the writers that are busy reserving this very row.
    // Only for lookups where "a few milliseconds old" is fine; reservations are decided by the atomic updates below.
    private IQueryable<StockItem> NoLockItems() =>
        dbContext.StockItems.FromSqlRaw("SELECT * FROM [inventory].[StockItems] WITH (NOLOCK)").AsNoTracking();

    public async Task AddAsync(StockItem item, CancellationToken cancellationToken) =>
        await dbContext.StockItems.AddAsync(item, cancellationToken);

    public Task<StockItem?> GetByIdAsync(Guid productId, CancellationToken cancellationToken) =>
        dbContext.StockItems.FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);

    public async Task<IReadOnlyList<StockItem>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        await NoLockItems().Where(item => productIds.Contains(item.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockItem>> ListAsync(CancellationToken cancellationToken) =>
        await NoLockItems().OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<bool> TryTakeAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var updated = await dbContext.StockItems
            .Where(item => item.Id == productId && item.AvailableQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AvailableQuantity, item => item.AvailableQuantity - quantity)
                    .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);

        return updated == 1;
    }

    public async Task ReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await dbContext.StockItems
            .Where(item => item.Id == productId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AvailableQuantity, item => item.AvailableQuantity + quantity)
                    .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);
    }
}

public class ReservationRepository(InventoryDbContext dbContext) : IReservationRepository
{
    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken) =>
        await dbContext.Reservations.AddAsync(reservation, cancellationToken);

    public Task<Reservation?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Reservations.Include(reservation => reservation.Lines)
            .FirstOrDefaultAsync(reservation => reservation.OrderId == orderId, cancellationToken);
}
