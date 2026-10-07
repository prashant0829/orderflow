namespace OrderFlow.Observability;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; } = true;

    /// <summary>OTLP gRPC endpoint of the OpenTelemetry collector.</summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>Share of new traces to keep (1 = all). Child spans always follow their parent's decision.</summary>
    public double SampleRatio { get; set; } = 1.0;

    /// <summary>How often metrics are sent (the OpenTelemetry default is 60 s, which makes dashboards feel stale).</summary>
    public int MetricsIntervalSeconds { get; set; } = 10;
}
