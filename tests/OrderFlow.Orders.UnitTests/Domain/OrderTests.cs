using OrderFlow.SharedKernel;
using FluentAssertions;
using OrderFlow.Orders.Domain.Common;
using OrderFlow.Orders.Domain.Orders;
using OrderFlow.Orders.Domain.Orders.Events;

namespace OrderFlow.Orders.UnitTests.Domain;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Order CreateValidOrder() =>
        Order.Create(CustomerId, "key-1", [(Guid.NewGuid(), 2, 10m), (Guid.NewGuid(), 1, 5.5m)]);

    [Fact]
    public void Create_ComputesTotalAndStartsPending()
    {
        var order = CreateValidOrder();

        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(25.5m);
        order.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Create_RaisesOrderCreatedEvent()
    {
        var order = CreateValidOrder();

        order.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Create_WithNoLines_Throws()
    {
        var act = () => Order.Create(CustomerId, "key-1", []);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -0.01)]
    public void Create_WithInvalidLine_Throws(int quantity, double unitPrice)
    {
        var act = () => Order.Create(CustomerId, "key-1", [(Guid.NewGuid(), quantity, (decimal)unitPrice)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_PendingOrder_SetsCancelledAndRaisesEvent()
    {
        var order = CreateValidOrder();

        order.Cancel("changed mind");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.FailureReason.Should().Be("changed mind");
        order.DomainEvents.Should().ContainItemsAssignableTo<OrderCancelledDomainEvent>();
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var order = CreateValidOrder();
        order.Cancel("first");

        var act = () => order.Cancel("second");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_AfterFail_Throws()
    {
        var order = CreateValidOrder();
        order.Fail("payment declined");

        var act = order.Confirm;

        act.Should().Throw<DomainException>();
    }
}
