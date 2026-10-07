using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderFlow.Observability;

public static class TelemetryServiceCollectionExtensions
{
    private static readonly string[] NoisyPaths = ["/health", "/swagger"];

    // MassTransit's own bookkeeping tables: polled constantly, and not interesting when reading a trace.
    private static readonly string[] BookkeepingTables = ["OutboxMessage", "OutboxState", "InboxState"];

    /// <summary>
    /// Traces (incoming HTTP, outgoing HTTP, SQL, MassTransit, handlers, cache) and metrics (HTTP, runtime, MassTransit, plus the
    /// service's own business meters) sent to the OpenTelemetry collector.
    /// </summary>
    public static IServiceCollection AddOrderFlowTelemetry(
        this IServiceCollection services, IConfiguration configuration, string serviceName, params string[] businessMeters)
    {
        var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
        if (!options.Enabled)
        {
            return services;
        }

        var endpoint = new Uri(options.OtlpEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
                .SetSampler(new DropOrphanClientSpansSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SampleRatio))))
                .AddAspNetCoreInstrumentation(aspNet =>
                {
                    aspNet.RecordException = true;
                    aspNet.Filter = context => !NoisyPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
                })
                .AddHttpClientInstrumentation(http => http.RecordException = true)
                .AddSqlClientInstrumentation(sql =>
                {
                    sql.RecordException = true;
                    sql.Filter = command => command is not Microsoft.Data.SqlClient.SqlCommand sqlCommand
                        || !BookkeepingTables.Any(table => sqlCommand.CommandText.Contains(table, StringComparison.Ordinal));
                })
                .AddSource(TelemetryNames.ActivitySources)
                .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(TelemetryNames.MassTransit)
                .AddMeter(businessMeters)
                .AddOtlpExporter((exporter, reader) =>
                {
                    exporter.Endpoint = endpoint;
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricsIntervalSeconds * 1000;
                }));

        return services;
    }
}
