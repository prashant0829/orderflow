using FluentAssertions;
using OrderFlow.Contracts.Orders;
using OrderFlow.Orders.Domain.Orders;
using OrderFlow.Orders.Infrastructure.Messaging;

namespace OrderFlow.Orders.UnitTests.Infrastructure;

public class IntegrationEventMapperTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    [Fact]
    public void OrderCreated_MapsToOrderSubmittedWithLinesAndTotal()
    {
        var order = Order.Create(Guid.NewGuid(), "key", [(ProductId, 3, 2.5m)]);

        var mapped = IntegrationEventMapper.ToIntegrationEvent(order.DomainEvents.Single());

        var submitted = mapped.Should().BeOfType<OrderSubmitted>().Subject;
        submitted.OrderId.Should().Be(order.Id);
        submitted.TotalAmount.Should().Be(7.5m);
        submitted.Lines.Should().ContainSingle().Which.Should().Be(new OrderFlow.Contracts.Orders.OrderLine(ProductId, 3, 2.5m));
    }

    [Fact]
    public void OrderCancelled_MapsToOrderCancelledWithReason()
    {
        var order = Order.Create(Guid.NewGuid(), "key", [(ProductId, 1, 1m)]);
        order.ClearDomainEvents();
        order.Cancel("too slow");

        var mapped = IntegrationEventMapper.ToIntegrationEvent(order.DomainEvents.Single());

        mapped.Should().BeEquivalentTo(new OrderCancelled(order.Id, order.CustomerId, "too slow"));
    }
}
