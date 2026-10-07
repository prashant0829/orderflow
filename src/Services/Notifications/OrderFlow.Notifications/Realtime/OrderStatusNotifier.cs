using Microsoft.AspNetCore.SignalR;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Notifications.Realtime;

public interface IOrderStatusNotifier
{
    Task PushAsync(OrderStatusChanged change, CancellationToken cancellationToken);
}

public class SignalROrderStatusNotifier(IHubContext<OrderHub> hubContext) : IOrderStatusNotifier
{
    public async Task PushAsync(OrderStatusChanged change, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Groups(RealtimeConstants.OrderGroup(change.OrderId), RealtimeConstants.CustomerGroup(change.CustomerId))
            .SendAsync(RealtimeConstants.OrderStatusChangedMethod, change, cancellationToken);
        NotificationsTelemetry.RecordPush(change.State);
    }
}
