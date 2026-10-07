namespace OrderFlow.Observability;

public static class TelemetryNames
{
    /// <summary>MassTransit publishes its own spans and metrics (consume duration, failures) under this name.</summary>
    public const string MassTransit = "MassTransit";

    public const string CqrsActivitySource = "OrderFlow.Cqrs";
    public const string CacheActivitySource = "OrderFlow.Cache";

    public static readonly string[] ActivitySources = [MassTransit, CqrsActivitySource, CacheActivitySource];
}
