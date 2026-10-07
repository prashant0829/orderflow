using OrderFlow.Orders.Domain.Common;

namespace OrderFlow.Orders.Domain.Orders.Events;

public record OrderCancelledDomainEvent(Guid OrderId, Guid CustomerId, string Reason) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}
