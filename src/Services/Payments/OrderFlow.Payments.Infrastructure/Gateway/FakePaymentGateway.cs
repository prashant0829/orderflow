using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;

namespace OrderFlow.Payments.Infrastructure.Gateway;

/// <summary>Stands in for Stripe/Razorpay. Behaviour is controlled by options and by two magic amounts, so demos are repeatable.</summary>
public class FakePaymentGateway(IOptions<FakePaymentGatewayOptions> options) : IPaymentGateway
{
    private readonly FakePaymentGatewayOptions _options = options.Value;

    public async Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken)
    {
        await SimulateNetworkAsync(amount, cancellationToken);

        var declined = amount == FakePaymentGatewayOptions.AlwaysDeclinedAmount || Roll(_options.DeclineRate);
        return declined
            ? GatewayChargeResult.Decline("Card declined by issuer.")
            : GatewayChargeResult.Approve($"txn_{Guid.NewGuid():N}");
    }

    public Task RefundAsync(string transactionId, CancellationToken cancellationToken) =>
        SimulateNetworkAsync(amount: 0, cancellationToken);

    private async Task SimulateNetworkAsync(decimal amount, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.LatencyMs, cancellationToken);

        if (amount == FakePaymentGatewayOptions.AlwaysUnavailableAmount || Roll(_options.TransientFailureRate))
        {
            throw new PaymentGatewayUnavailableException("Simulated gateway outage (503).");
        }
    }

    private static bool Roll(double probability) => probability > 0 && Random.Shared.NextDouble() < probability;
}
