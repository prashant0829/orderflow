using System.Net;
using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using Stripe;

namespace OrderFlow.Payments.Infrastructure.Gateway.Stripe;

/// <summary>
/// Stripe in test mode. A charge creates a PaymentIntent and the customer pays in the browser with Stripe's card form,
/// so the money is only confirmed later (webhook, or asking Stripe). Every call carries an idempotency key, so a retry
/// (ours or the message broker's) can never create a second payment or refund.
/// </summary>
public class StripePaymentGateway(IStripeClient client, IOptions<StripeOptions> stripeOptions, IOptions<PaymentsOptions> paymentsOptions) : IPaymentGateway
{
    private const string UnexpectedStateCode = "payment_intent_unexpected_state";
    private const string AlreadyRefundedCode = "charge_already_refunded";

    private readonly PaymentIntentService _intents = new(client);
    private readonly RefundService _refunds = new(client);

    public async Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        var intent = await ExecuteAsync(() => _intents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = ToMinorUnits(amount),
                Currency = paymentsOptions.Value.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                Description = $"OrderFlow order {orderId}",
                Metadata = new Dictionary<string, string> { ["orderId"] = orderId.ToString() }
            },
            new RequestOptions { IdempotencyKey = $"orderflow-payment-{orderId}" },
            cancellationToken));

        return GatewayChargeResult.RequireAction(intent.Id);
    }

    public async Task RefundAsync(string transactionId, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(() => _refunds.CreateAsync(
                new RefundCreateOptions { PaymentIntent = transactionId },
                new RequestOptions { IdempotencyKey = $"orderflow-refund-{transactionId}" },
                cancellationToken));
        }
        catch (StripeException exception) when (exception.StripeError?.Code == AlreadyRefundedCode)
        {
            // Done earlier (for example the first attempt succeeded but its answer was lost).
        }
    }

    public async Task<bool> CancelAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(() => _intents.CancelAsync(providerPaymentId, cancellationToken: cancellationToken));
            return true;
        }
        catch (StripeException exception) when (exception.StripeError?.Code == UnexpectedStateCode)
        {
            // Already cancelled counts as done. Succeeded or processing means the customer paid just now: do not cancel.
            return await GetStatusAsync(providerPaymentId, cancellationToken) == ProviderPaymentState.Canceled;
        }
    }

    public async Task<PaymentCheckout?> GetCheckoutAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        var intent = await ExecuteAsync(() => _intents.GetAsync(providerPaymentId, cancellationToken: cancellationToken));
        return new PaymentCheckout(PaymentsOptions.StripeProvider, stripeOptions.Value.PublishableKey, intent.ClientSecret, intent.Currency);
    }

    public async Task<ProviderPaymentState> GetStatusAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        var intent = await ExecuteAsync(() => _intents.GetAsync(providerPaymentId, cancellationToken: cancellationToken));
        return intent.Status switch
        {
            "succeeded" => ProviderPaymentState.Succeeded,
            "canceled" => ProviderPaymentState.Canceled,
            "processing" => ProviderPaymentState.Processing,
            _ => ProviderPaymentState.RequiresPayment
        };
    }

    /// <summary>Stripe's smallest unit is the cent. (Currencies without cents, like JPY, would need their own rule.)</summary>
    public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Network problems, rate limits and Stripe outages are temporary and get retried; anything else (bad request, bad key) is our bug and surfaces as an error.</summary>
    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (StripeException exception) when (IsTemporary(exception))
        {
            throw new PaymentGatewayUnavailableException($"Stripe is temporarily unavailable ({(int)exception.HttpStatusCode}).", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayUnavailableException("Stripe could not be reached.", exception);
        }
    }

    private static bool IsTemporary(StripeException exception) =>
        exception.HttpStatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests or 0
        || exception.StripeError is null && exception.InnerException is HttpRequestException;
}
