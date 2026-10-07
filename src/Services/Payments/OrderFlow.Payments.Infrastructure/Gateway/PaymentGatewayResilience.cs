using Microsoft.Extensions.Logging;
using OrderFlow.Payments.Application.Common;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace OrderFlow.Payments.Infrastructure.Gateway;

public static class PaymentGatewayResilience
{
    /// <summary>Order matters: the outer retry wraps the circuit breaker, which wraps a per-attempt timeout.</summary>
    public static ResiliencePipeline Create(PaymentResilienceOptions options, ILogger logger)
    {
        var builder = new ResiliencePipelineBuilder();

        // Polly rejects zero retries, so "no retries" means leaving the strategy out.
        if (options.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.RetryCount,
                Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<PaymentGatewayUnavailableException>().Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    logger.LogWarning("Payment gateway call failed, retry {Attempt} in {Delay} ms", args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            });
        }

        return builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<PaymentGatewayUnavailableException>().Handle<TimeoutRejectedException>(),
                OnOpened = _ =>
                {
                    PaymentsTelemetry.RecordCircuitOpened();
                    logger.LogError("Payment gateway circuit OPENED: calls fail fast for {Seconds} s", options.BreakDurationSeconds);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Payment gateway circuit closed again");
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds))
            .Build();
    }
}
