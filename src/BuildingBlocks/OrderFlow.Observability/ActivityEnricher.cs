using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace OrderFlow.Observability;

/// <summary>Adds TraceId and SpanId to every log line, so a log entry in Seq can be matched to its trace in Jaeger.</summary>
public class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
