using MassTransit;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts.Orders;
using OrderFlow.Notifications.Sending;

namespace OrderFlow.Notifications.Consumers;

public class OrderSubmittedConsumer(
    INotificationService notifications,
    IOptions<NotificationOptions> options) : IConsumer<OrderSubmitted>
{
    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var order = context.Message;

        if (options.Value.PoisonCustomerId == order.CustomerId)
        {
            throw new InvalidOperationException($"Simulated failure for poison customer {order.CustomerId}.");
        }

        return notifications.SendAsync(
            order.OrderId, order.CustomerId,
            NotificationTemplates.OrderReceived(order.OrderId, order.Lines.Count, order.TotalAmount),
            context.CancellationToken);
    }
}
