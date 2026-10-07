using MassTransit;
using OrderFlow.Contracts.Orders;
using OrderFlow.Notifications.Sending;

namespace OrderFlow.Notifications.Consumers;

public class OrderConfirmedConsumer(INotificationService notifications) : IConsumer<OrderConfirmed>
{
    public Task Consume(ConsumeContext<OrderConfirmed> context) => notifications.SendAsync(
        context.Message.OrderId, context.Message.CustomerId,
        NotificationTemplates.OrderConfirmed(context.Message.OrderId), context.CancellationToken);
}

public class OrderFailedConsumer(INotificationService notifications) : IConsumer<OrderFailed>
{
    public Task Consume(ConsumeContext<OrderFailed> context) => notifications.SendAsync(
        context.Message.OrderId, context.Message.CustomerId,
        NotificationTemplates.OrderFailed(context.Message.OrderId, context.Message.Reason), context.CancellationToken);
}

public class OrderCancelledConsumer(INotificationService notifications) : IConsumer<OrderCancelled>
{
    public Task Consume(ConsumeContext<OrderCancelled> context) => notifications.SendAsync(
        context.Message.OrderId, context.Message.CustomerId,
        NotificationTemplates.OrderCancelled(context.Message.OrderId, context.Message.Reason), context.CancellationToken);
}
