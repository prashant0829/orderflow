namespace OrderFlow.Payments.Infrastructure.Gateway;

public class PaymentResilienceOptions
{
    public const string SectionName = "PaymentResilience";

    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int AttemptTimeoutSeconds { get; set; } = 5;
    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 6;
    public int SamplingDurationSeconds { get; set; } = 30;
    public int BreakDurationSeconds { get; set; } = 20;
}
