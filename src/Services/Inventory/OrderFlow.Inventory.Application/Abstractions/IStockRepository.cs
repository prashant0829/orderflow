using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.Application.Abstractions;

public interface IStockRepository
{
    Task AddAsync(StockItem item, CancellationToken cancellationToken);

    /// <summary>Loads one item for changing (restock). Tracked.</summary>
    Task<StockItem?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only snapshot that never waits for writers. Used for prices, names and quick "is there enough" checks,
    /// where a value that is a few milliseconds old does not matter. Never use it to decide a reservation.
    /// </summary>
    Task<IReadOnlyList<StockItem>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockItem>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Takes the quantity in one atomic statement ("subtract N where at least N is left"). Many orders for the same
    /// product queue up on the row lock instead of colliding and retrying. Returns false if there is not enough.
    /// </summary>
    Task<bool> TryTakeAsync(Guid productId, int quantity, CancellationToken cancellationToken);

    /// <summary>Gives quantity back in one atomic statement (release, or undoing part of a failed multi-line reservation).</summary>
    Task ReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken);
}
