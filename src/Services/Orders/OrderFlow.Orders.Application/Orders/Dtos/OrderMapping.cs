using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Application.Orders.Dtos;

public static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString(),
        order.TotalAmount,
        order.FailureReason,
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.Lines.Select(line => new OrderLineDto(line.ProductId, line.Quantity, line.UnitPrice, line.LineTotal)).ToList());
}
