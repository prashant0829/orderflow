using System.Collections.Concurrent;
using OrderFlow.Payments.Application.Abstractions;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Behaves like Stripe from the Payments service's point of view: creating a payment does not take any money, the customer pays
/// later (the test calls <see cref="CustomerPays"/>), and the provider can be asked where the payment stands.
/// </summary>
public sealed class CardGatewayDouble : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, ProviderPaymentState> _payments = new();
    private readonly ConcurrentDictionary<string, bool> _cannotCancel = new();

    public ConcurrentQueue<string> Cancelled { get; } = new();

    public ConcurrentQueue<string> Refunded { get; } = new();

    public static string PaymentIdFor(Guid orderId) => $"pi_test_{orderId:N}";

    public void CustomerPays(string providerPaymentId) => _payments[providerPaymentId] = ProviderPaymentState.Succeeded;

    /// <summary>From now on the provider says "too late to cancel", as if the customer had just paid.</summary>
    public void RefuseCancel(string providerPaymentId) => _cannotCancel[providerPaymentId] = true;

    public Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        var id = PaymentIdFor(orderId);
        _payments.TryAdd(id, ProviderPaymentState.RequiresPayment);
        return Task.FromResult(GatewayChargeResult.RequireAction(id));
    }

    public Task RefundAsync(string transactionId, CancellationToken cancellationToken)
    {
        Refunded.Enqueue(transactionId);
        return Task.CompletedTask;
    }

    public Task<bool> CancelAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        if (_cannotCancel.ContainsKey(providerPaymentId))
        {
            return Task.FromResult(false);
        }

        _payments[providerPaymentId] = ProviderPaymentState.Canceled;
        Cancelled.Enqueue(providerPaymentId);
        return Task.FromResult(true);
    }

    public Task<PaymentCheckout?> GetCheckoutAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult<PaymentCheckout?>(new PaymentCheckout("Stripe", "pk_test_double", $"{providerPaymentId}_secret_double", "usd"));

    public Task<ProviderPaymentState> GetStatusAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.GetValueOrDefault(providerPaymentId, ProviderPaymentState.RequiresPayment));
}
