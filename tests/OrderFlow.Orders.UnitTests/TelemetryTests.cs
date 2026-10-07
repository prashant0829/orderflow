using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using MediatR;
using OpenTelemetry.Trace;
using OrderFlow.Cqrs;
using OrderFlow.Observability;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.UnitTests;

public class TelemetryTests
{
    private sealed class AlwaysSample : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) => new(SamplingDecision.RecordAndSample);
    }

    private static SamplingParameters Parameters(ActivityKind kind, bool hasParent)
    {
        var parent = hasParent
            ? new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            : default;
        return new SamplingParameters(parent, ActivityTraceId.CreateRandom(), "span", kind);
    }

    [Fact]
    public void Sampler_DropsRootClientSpans_ButKeepsEverythingElse()
    {
        var sampler = new DropOrphanClientSpansSampler(new AlwaysSample());

        sampler.ShouldSample(Parameters(ActivityKind.Client, hasParent: false)).Decision.Should().Be(SamplingDecision.Drop);
        sampler.ShouldSample(Parameters(ActivityKind.Client, hasParent: true)).Decision.Should().Be(SamplingDecision.RecordAndSample);
        sampler.ShouldSample(Parameters(ActivityKind.Server, hasParent: false)).Decision.Should().Be(SamplingDecision.RecordAndSample);
        sampler.ShouldSample(Parameters(ActivityKind.Consumer, hasParent: false)).Decision.Should().Be(SamplingDecision.RecordAndSample);
    }

    [Fact]
    public async Task TracingBehavior_CreatesASpanNamedAfterTheRequest()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TracingBehavior<Ping, string>.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);

        await new TracingBehavior<Ping, string>().Handle(new Ping(), () => Task.FromResult("pong"), CancellationToken.None);

        spans.Should().ContainSingle().Which.DisplayName.Should().Be("Handle Ping");
    }

    [Fact]
    public async Task TracingBehavior_MarksTheSpanAsErrorWhenTheHandlerThrows()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TracingBehavior<Ping, string>.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);

        var act = () => new TracingBehavior<Ping, string>().Handle(new Ping(), () => throw new InvalidOperationException("boom"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        spans.Single(span => span.DisplayName == "Handle Ping").Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void OrdersTelemetry_RecordsCompletionWithOutcomeAndDuration()
    {
        var counts = new List<(long Value, string? Outcome)>();
        var durations = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OrdersTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "orderflow.orders.completed")
            {
                counts.Add((value, tags.ToArray().FirstOrDefault(tag => tag.Key == "outcome").Value as string));
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            if (instrument.Name == "orderflow.orders.duration")
            {
                durations.Add(value);
            }
        });
        listener.Start();

        OrdersTelemetry.RecordCompleted(OrdersTelemetry.Failed, DateTime.UtcNow.AddSeconds(-3));

        counts.Should().Contain((1, OrdersTelemetry.Failed));
        durations.Should().ContainSingle().Which.Should().BeGreaterThan(2.5);
    }

    private sealed record Ping : IRequest<string>;
}
