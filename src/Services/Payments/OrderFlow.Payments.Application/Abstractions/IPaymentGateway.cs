namespace OrderFlow.Payments.Application.Abstractions;

public enum ChargeOutcome
{
    /// <summary>Money taken immediately (the fake gateway).</summary>
    Approved,

    /// <summary>Refused immediately.</summary>
    Declined,

    /// <summary>A payment was created and the customer still has to pay (Stripe: enter card details). The result then arrives by webhook or by asking the provider.</summary>
    ActionRequired
}

public record GatewayChargeResult(ChargeOutcome Outcome, string? TransactionId, string? DeclineReason)
{
    public bool Approved => Outcome == ChargeOutcome.Approved;

    public static GatewayChargeResult Approve(string transactionId) => new(ChargeOutcome.Approved, transactionId, null);

    public static GatewayChargeResult Decline(string reason) => new(ChargeOutcome.Declined, null, reason);

    /// <param name="providerPaymentId">The provider's id for the new payment.</param>
    public static GatewayChargeResult RequireAction(string providerPaymentId) => new(ChargeOutcome.ActionRequired, providerPaymentId, null);
}

/// <summary>What the browser needs to show the provider's payment form.</summary>
public record PaymentCheckout(string Provider, string? PublishableKey, string ClientSecret, string Currency);

public enum ProviderPaymentState
{
    RequiresPayment,
    Processing,
    Succeeded,
    Canceled
}

public interface IPaymentGateway
{
    /// <summary>Starts a payment for the order. Must be safe to call twice for the same order (providers take an idempotency key).</summary>
    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);

    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task RefundAsync(string transactionId, CancellationToken cancellationToken);

    /// <summary>Cancels a payment the customer has not completed. Returns false if the provider says it can no longer be cancelled (for example the customer just paid).</summary>
    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task<bool> CancelAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>The data for the payment form, or null for providers that never ask the customer for anything.</summary>
    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task<PaymentCheckout?> GetCheckoutAsync(string providerPaymentId, CancellationToken cancellationToken);

    /// <summary>Asks the provider where the payment stands. Used when a webhook is late or not available.</summary>
    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task<ProviderPaymentState> GetStatusAsync(string providerPaymentId, CancellationToken cancellationToken);
}
