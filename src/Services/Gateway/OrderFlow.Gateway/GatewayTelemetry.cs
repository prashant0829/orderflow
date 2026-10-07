using System.Diagnostics.Metrics;
using OrderFlow.Gateway.RateLimiting;

namespace OrderFlow.Gateway;

public static class GatewayTelemetry
{
    public const string MeterName = "OrderFlow.Gateway";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> RateLimitChecks = Meter.CreateCounter<long>("orderflow.gateway.ratelimit.checks", "{check}", "Rate limit decisions");

    // Counters start at zero on startup. Prometheus cannot see the first increment of a series that first appears already at 1,
    // so without this the first order of each kind would be missing from rate() and increase().
    static GatewayTelemetry()
    {
        foreach (var policy in new[] { RateLimitOptions.AuthPolicy, RateLimitOptions.AuthenticatedPolicy, RateLimitOptions.AnonymousPolicy })
        {
            foreach (var allowed in new[] { true, false })
            {
                RateLimitChecks.Add(0, new KeyValuePair<string, object?>("policy", policy), new KeyValuePair<string, object?>("allowed", allowed));
            }
        }
    }

    public static void RecordRateLimit(string policy, bool allowed) =>
        RateLimitChecks.Add(1, new KeyValuePair<string, object?>("policy", policy), new KeyValuePair<string, object?>("allowed", allowed));
}
