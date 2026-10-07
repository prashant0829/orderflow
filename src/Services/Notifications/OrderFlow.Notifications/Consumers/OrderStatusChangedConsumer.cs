using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts.Orders;
using OrderFlow.Notifications.Persistence;
using OrderFlow.Notifications.Realtime;

namespace OrderFlow.Notifications.Consumers;

/// <summary>Stores each step for the timeline, then pushes it to connected browsers.</summary>
public class OrderStatusChangedConsumer(
    NotificationsDbContext dbContext,
    IOrderStatusNotifier notifier) : IConsumer<OrderStatusChanged>
{
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var change = context.Message;

        var alreadyStored = await dbContext.TimelineEntries
            .AnyAsync(entry => entry.OrderId == change.OrderId && entry.State == change.State, context.CancellationToken);

        if (!alreadyStored)
        {
            dbContext.TimelineEntries.Add(new OrderTimelineEntry
            {
                OrderId = change.OrderId,
                CustomerId = change.CustomerId,
                State = change.State,
                Message = change.Message,
                OccurredAtUtc = change.OccurredAtUtc
            });
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        await notifier.PushAsync(change, context.CancellationToken);
    }
}
