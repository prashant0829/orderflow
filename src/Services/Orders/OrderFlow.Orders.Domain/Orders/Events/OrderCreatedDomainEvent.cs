using OrderFlow.Orders.Domain.Common;

namespace OrderFlow.Orders.Domain.Orders.Events;

public record OrderLineSnapshot(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderCreatedDomainEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, IReadOnlyList<OrderLineSnapshot> Lines)
    : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}
