using System.Diagnostics.Metrics;

namespace OrderFlow.Orders.Application.Common;

public static class OrdersTelemetry
{
    public const string MeterName = "OrderFlow.Orders";

    public const string Confirmed = "confirmed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OrdersCreated = Meter.CreateCounter<long>("orderflow.orders.created", "{order}", "Orders accepted");
    private static readonly Counter<long> OrdersCompleted = Meter.CreateCounter<long>("orderflow.orders.completed", "{order}", "Orders that reached a final state");
    private static readonly Histogram<double> OrderDuration = Meter.CreateHistogram<double>("orderflow.orders.duration", "s", "Seconds from order creation to its final state");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static OrdersTelemetry()
    {
        OrdersCreated.Add(0);
        foreach (var outcome in new[] { Confirmed, Failed, Cancelled })
        {
            OrdersCompleted.Add(0, new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    public static void RecordCreated() => OrdersCreated.Add(1);

    public static void RecordCompleted(string outcome, DateTime createdAtUtc)
    {
        OrdersCompleted.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        OrderDuration.Record(Math.Max((DateTime.UtcNow - createdAtUtc).TotalSeconds, 0), new KeyValuePair<string, object?>("outcome", outcome));
    }
}
