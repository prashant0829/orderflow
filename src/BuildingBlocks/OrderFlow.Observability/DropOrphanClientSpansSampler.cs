using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OrderFlow.Observability;

/// <summary>
/// Background loops (MassTransit outbox delivery, inbox cleanup) run SQL on their own with no request behind it, which would flood
/// the trace list with one-span traces. Client spans without a parent are dropped; everything that belongs to a real
/// request keeps its parent and is sampled normally.
/// </summary>
public class DropOrphanClientSpansSampler(Sampler inner) : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var isRoot = samplingParameters.ParentContext.TraceId == default;
        return isRoot && samplingParameters.Kind == ActivityKind.Client
            ? new SamplingResult(SamplingDecision.Drop)
            : inner.ShouldSample(samplingParameters);
    }
}
