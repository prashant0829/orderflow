namespace OrderFlow.Payments.Infrastructure.Gateway;

public class FakePaymentGatewayOptions
{
    public const string SectionName = "FakePaymentGateway";

    /// <summary>Amount that is always declined (card declined).</summary>
    public const decimal AlwaysDeclinedAmount = 13.13m;

    /// <summary>Amount that always fails with a temporary outage. Triggers retries and the circuit breaker.</summary>
    public const decimal AlwaysUnavailableAmount = 66.60m;

    /// <summary>Chance (0 to 1) that a charge is declined.</summary>
    public double DeclineRate { get; set; }

    /// <summary>Chance (0 to 1) that a call fails with a temporary outage.</summary>
    public double TransientFailureRate { get; set; }

    public int LatencyMs { get; set; } = 50;
}
