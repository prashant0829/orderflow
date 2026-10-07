namespace OrderFlow.Contracts.Orders;

public record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderSubmitted(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderLine> Lines, decimal TotalAmount);

public record OrderConfirmed(Guid OrderId, Guid CustomerId);

public record OrderFailed(Guid OrderId, Guid CustomerId, string Reason);

public record OrderCancelled(Guid OrderId, Guid CustomerId, string Reason);

/// <summary>Published by the saga at every step, so UIs and notifications can show live progress.</summary>
public record OrderStatusChanged(Guid OrderId, Guid CustomerId, string State, string Message, DateTime OccurredAtUtc);

public static class OrderProgressStates
{
    public const string AwaitingStock = "AwaitingStock";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Confirmed = "Confirmed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}
