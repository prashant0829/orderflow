using System.Diagnostics.Metrics;

namespace OrderFlow.Inventory.Application.Common;

public static class InventoryTelemetry
{
    public const string MeterName = "OrderFlow.Inventory";

    public const string Reserved = "reserved";
    public const string Rejected = "rejected";
    public const string Duplicate = "duplicate";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Reservations = Meter.CreateCounter<long>("orderflow.stock.reservations", "{reservation}", "Stock reservation attempts by result");
    private static readonly Counter<long> Releases = Meter.CreateCounter<long>("orderflow.stock.releases", "{release}", "Reservations released (compensation)");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static InventoryTelemetry()
    {
        foreach (var result in new[] { Reserved, Rejected, Duplicate })
        {
            Reservations.Add(0, new KeyValuePair<string, object?>("result", result));
        }

        Releases.Add(0);
    }

    public static void RecordReservation(string result) => Reservations.Add(1, new KeyValuePair<string, object?>("result", result));

    public static void RecordRelease() => Releases.Add(1);
}
