using OrderFlow.Contracts.Orders;
using OrderFlow.Orders.Domain.Common;
using OrderFlow.Orders.Domain.Orders.Events;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public static class IntegrationEventMapper
{
    public static object? ToIntegrationEvent(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderCreatedDomainEvent created => new OrderSubmitted(
            created.OrderId,
            created.CustomerId,
            created.Lines.Select(line => new OrderLine(line.ProductId, line.Quantity, line.UnitPrice)).ToList(),
            created.TotalAmount),
        OrderCancelledDomainEvent cancelled => new OrderCancelled(cancelled.OrderId, cancelled.CustomerId, cancelled.Reason),
        _ => null
    };
}
