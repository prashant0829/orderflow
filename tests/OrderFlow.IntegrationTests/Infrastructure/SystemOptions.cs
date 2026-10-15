namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>Knobs that differ between test collections (set as environment variables before the services start).</summary>
public record SystemOptions
{
    public int PaymentLatencyMs { get; init; } = 20;
    public int SagaPaymentTimeoutSeconds { get; init; } = 60;

    /// <summary>Payments behave like Stripe: the customer has to pay later, and the result arrives by webhook or when asked.</summary>
    public bool CustomerPaysByCard { get; init; }
}
