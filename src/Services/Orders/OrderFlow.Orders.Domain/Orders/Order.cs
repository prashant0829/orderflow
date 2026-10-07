using OrderFlow.SharedKernel;
using OrderFlow.Orders.Domain.Common;
using OrderFlow.Orders.Domain.Orders.Events;

namespace OrderFlow.Orders.Domain.Orders;

public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    public Guid CustomerId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<OrderLine> Lines => _lines;

    public static Order Create(Guid customerId, string idempotencyKey, IEnumerable<(Guid ProductId, int Quantity, decimal UnitPrice)> lines)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency key is required.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            IdempotencyKey = idempotencyKey,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        order.UpdatedAtUtc = order.CreatedAtUtc;

        foreach (var (productId, quantity, unitPrice) in lines)
        {
            order._lines.Add(OrderLine.Create(productId, quantity, unitPrice));
        }

        if (order._lines.Count == 0)
        {
            throw new DomainException("An order needs at least one line.");
        }

        order.TotalAmount = order._lines.Sum(line => line.LineTotal);
        order.Raise(new OrderCreatedDomainEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order._lines.Select(line => new OrderLineSnapshot(line.ProductId, line.Quantity, line.UnitPrice)).ToList()));
        return order;
    }

    public void Cancel(string reason)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException($"Only pending orders can be cancelled. Current status: {Status}.");
        }

        Status = OrderStatus.Cancelled;
        FailureReason = reason;
        Touch();
        Raise(new OrderCancelledDomainEvent(Id, CustomerId, reason));
    }

    public void Confirm()
    {
        EnsurePending(nameof(Confirm));
        Status = OrderStatus.Confirmed;
        Touch();
    }

    public void Fail(string reason)
    {
        EnsurePending(nameof(Fail));
        Status = OrderStatus.Failed;
        FailureReason = reason;
        Touch();
    }

    private void EnsurePending(string action)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException($"Cannot {action} an order in status {Status}.");
        }
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
