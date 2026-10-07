using System.Diagnostics.Metrics;

namespace OrderFlow.Payments.Application.Common;

public static class PaymentsTelemetry
{
    public const string MeterName = "OrderFlow.Payments";

    public const string Completed = "completed";
    public const string Declined = "declined";
    public const string GatewayUnavailable = "gateway_unavailable";
    public const string Duplicate = "duplicate";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Charges = Meter.CreateCounter<long>("orderflow.payments.charges", "{charge}", "Charge attempts by result");
    private static readonly Counter<long> Refunds = Meter.CreateCounter<long>("orderflow.payments.refunds", "{refund}", "Refunds issued");
    private static readonly Counter<long> CircuitOpened = Meter.CreateCounter<long>("orderflow.payments.circuit.opened", "{event}", "Times the payment gateway circuit breaker opened");
    private static readonly Histogram<double> GatewayDuration = Meter.CreateHistogram<double>("orderflow.payments.gateway.duration", "s", "Payment gateway call duration (including retries)");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static PaymentsTelemetry()
    {
        foreach (var result in new[] { Completed, Declined, GatewayUnavailable, Duplicate })
        {
            Charges.Add(0, new KeyValuePair<string, object?>("result", result));
        }

        Refunds.Add(0);
        CircuitOpened.Add(0);
    }

    public static void RecordCharge(string result) => Charges.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void RecordRefund() => Refunds.Add(1);

    public static void RecordCircuitOpened() => CircuitOpened.Add(1);

    public static void RecordGatewayCall(string operation, bool succeeded, TimeSpan duration) =>
        GatewayDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", succeeded ? "ok" : "error"));
}
