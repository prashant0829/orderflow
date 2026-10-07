using System.Diagnostics.Metrics;
using OrderFlow.Contracts.Orders;
using OrderFlow.Notifications.Persistence;

namespace OrderFlow.Notifications;

public static class NotificationsTelemetry
{
    public const string MeterName = "OrderFlow.Notifications";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Sent = Meter.CreateCounter<long>("orderflow.notifications.sent", "{notification}", "Notifications sent by type");
    private static readonly Counter<long> Pushes = Meter.CreateCounter<long>("orderflow.realtime.pushes", "{push}", "Status updates pushed to browsers");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static NotificationsTelemetry()
    {
        foreach (var type in new[] { NotificationTypes.OrderReceived, NotificationTypes.OrderConfirmed, NotificationTypes.OrderFailed, NotificationTypes.OrderCancelled })
        {
            Sent.Add(0, new KeyValuePair<string, object?>("type", type));
        }

        foreach (var state in new[] { OrderProgressStates.AwaitingStock, OrderProgressStates.AwaitingPayment, OrderProgressStates.Confirmed, OrderProgressStates.Failed, OrderProgressStates.Cancelled })
        {
            Pushes.Add(0, new KeyValuePair<string, object?>("state", state));
        }
    }

    public static void RecordSent(string type) => Sent.Add(1, new KeyValuePair<string, object?>("type", type));

    public static void RecordPush(string state) => Pushes.Add(1, new KeyValuePair<string, object?>("state", state));
}
