namespace OrderFlow.Payments.Infrastructure.Gateway;

public class PaymentsOptions
{
    public const string SectionName = "Payments";
    public const string FakeProvider = "Fake";
    public const string StripeProvider = "Stripe";

    /// <summary>"Fake" (instant, simulated) or "Stripe" (real sandbox, customer enters a test card).</summary>
    public string Provider { get; set; } = FakeProvider;

    /// <summary>Currency charged at the provider (ISO code). Stripe test mode supports usd out of the box.</summary>
    public string Currency { get; set; } = "usd";
}
