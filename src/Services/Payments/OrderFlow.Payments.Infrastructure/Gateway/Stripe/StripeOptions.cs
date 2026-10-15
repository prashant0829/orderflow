namespace OrderFlow.Payments.Infrastructure.Gateway.Stripe;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>sk_test_... from the Stripe dashboard (test mode). Keep it out of git: user secrets or an environment variable.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>pk_test_... Safe to show to browsers; the order page uses it to load the card form.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>whsec_... Printed by "stripe listen" or shown on the webhook endpoint in the dashboard. Without it the webhook endpoint is off.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Override the API address, for example http://localhost:12111 to talk to Stripe's local mock server.</summary>
    public string? ApiBase { get; set; }
}
