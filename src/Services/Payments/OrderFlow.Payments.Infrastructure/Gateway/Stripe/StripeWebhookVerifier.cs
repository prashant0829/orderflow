using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using Stripe;

namespace OrderFlow.Payments.Infrastructure.Gateway.Stripe;

/// <summary>Anyone can post to a webhook URL, so every call is checked against Stripe's signature (HMAC with the endpoint secret, with a time limit against replays).</summary>
public class StripeWebhookVerifier(IOptions<StripeOptions> options) : IWebhookVerifier
{
    private const string Succeeded = "payment_intent.succeeded";
    private const string PaymentFailed = "payment_intent.payment_failed";
    private const string Canceled = "payment_intent.canceled";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.WebhookSecret);

    public ProviderPaymentUpdate? Verify(string body, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new InvalidWebhookSignatureException("The Stripe-Signature header is missing.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(body, signatureHeader, options.Value.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            throw new InvalidWebhookSignatureException("The webhook signature is not valid.", exception);
        }

        if (stripeEvent.Data.Object is not PaymentIntent intent)
        {
            return null;
        }

        return stripeEvent.Type switch
        {
            Succeeded => new ProviderPaymentUpdate(intent.Id, ProviderPaymentOutcome.Succeeded),
            PaymentFailed => new ProviderPaymentUpdate(intent.Id, ProviderPaymentOutcome.Failed),
            Canceled => new ProviderPaymentUpdate(intent.Id, ProviderPaymentOutcome.Canceled),
            _ => null
        };
    }
}

/// <summary>Used when the provider has no webhooks (the fake gateway).</summary>
public class DisabledWebhookVerifier : IWebhookVerifier
{
    public bool IsEnabled => false;

    public ProviderPaymentUpdate? Verify(string body, string? signatureHeader) => null;
}
