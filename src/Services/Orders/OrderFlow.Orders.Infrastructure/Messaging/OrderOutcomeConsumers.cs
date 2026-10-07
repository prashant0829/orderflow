using MassTransit;
using MediatR;
using OrderFlow.Contracts.Orders;
using OrderFlow.Orders.Application.Orders.Commands;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public class OrderConfirmedConsumer(ISender sender) : IConsumer<OrderConfirmed>
{
    public Task Consume(ConsumeContext<OrderConfirmed> context) =>
        sender.Send(new ConfirmOrderCommand(context.Message.OrderId), context.CancellationToken);
}

public class OrderFailedConsumer(ISender sender) : IConsumer<OrderFailed>
{
    public Task Consume(ConsumeContext<OrderFailed> context) =>
        sender.Send(new FailOrderCommand(context.Message.OrderId, context.Message.Reason), context.CancellationToken);
}
