namespace OrderFlow.Payments.Application.Abstractions;

public enum ProviderPaymentOutcome
{
    Succeeded,
    Failed,
    Canceled
}

public record ProviderPaymentUpdate(string ProviderPaymentId, ProviderPaymentOutcome Outcome);

public interface IWebhookVerifier
{
    /// <summary>False when no webhook secret is configured: the endpoint is then switched off.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Checks the signature and turns the provider's event into an update, or null for event types we do not care about.
    /// </summary>
    /// <exception cref="Common.InvalidWebhookSignatureException">The signature is missing or wrong (the request did not come from the provider).</exception>
    ProviderPaymentUpdate? Verify(string body, string? signatureHeader);
}
