using OrderFlow.Contracts.Orders;

namespace OrderFlow.Contracts.Inventory;

public record ReserveStock(Guid OrderId, IReadOnlyList<OrderLine> Lines);

public record StockReserved(Guid OrderId);

public record StockReservationFailed(Guid OrderId, string Reason);

public record ReleaseStock(Guid OrderId);
