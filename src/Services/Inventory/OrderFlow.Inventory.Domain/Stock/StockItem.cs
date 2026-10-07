using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.Domain.Stock;

public class StockItem
{
    private StockItem()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int AvailableQuantity { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token: two parallel reservations cannot both succeed on stale data.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static StockItem Create(Guid id, string name, decimal unitPrice, int initialQuantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.");
        }

        if (initialQuantity < 0)
        {
            throw new DomainException("Initial quantity cannot be negative.");
        }

        return new StockItem
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            Name = name.Trim(),
            UnitPrice = unitPrice,
            AvailableQuantity = initialQuantity,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public bool CanReserve(int quantity) => quantity > 0 && AvailableQuantity >= quantity;

    public void Reserve(int quantity)
    {
        if (!CanReserve(quantity))
        {
            throw new DomainException($"Cannot reserve {quantity} of '{Name}'. Available: {AvailableQuantity}.");
        }

        AvailableQuantity -= quantity;
        Touch();
    }

    public void Release(int quantity)
    {
        EnsurePositive(quantity);
        AvailableQuantity += quantity;
        Touch();
    }

    public void Restock(int quantity) => Release(quantity);

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
