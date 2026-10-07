using OrderFlow.Orders.Application.Orders.Commands;

namespace OrderFlow.Orders.Api.Contracts;

public record CreateOrderRequest(IReadOnlyList<OrderLineInput> Lines);

public record CancelOrderRequest(string Reason);
