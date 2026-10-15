using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrderFlow.Payments.Infrastructure.Gateway;

/// <summary>Decorator: adds retry, circuit breaker and timeout around any gateway without changing it (Open/Closed).</summary>
public class ResilientPaymentGateway(IPaymentGateway inner, ResiliencePipeline pipeline) : IPaymentGateway
{
    public Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken) =>
        ExecuteAsync("charge", token => inner.ChargeAsync(orderId, amount, token), cancellationToken);

    public Task RefundAsync(string transactionId, CancellationToken cancellationToken) =>
        ExecuteAsync("refund", async token =>
        {
            await inner.RefundAsync(transactionId, token);
            return true;
        }, cancellationToken);

    public Task<bool> CancelAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        ExecuteAsync("cancel", token => inner.CancelAsync(providerPaymentId, token), cancellationToken);

    public Task<PaymentCheckout?> GetCheckoutAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        ExecuteAsync("checkout", token => inner.GetCheckoutAsync(providerPaymentId, token), cancellationToken);

    public Task<ProviderPaymentState> GetStatusAsync(string providerPaymentId, CancellationToken cancellationToken) =>
        ExecuteAsync("status", token => inner.GetStatusAsync(providerPaymentId, token), cancellationToken);

    private async Task<T> ExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var succeeded = false;
        try
        {
            var result = await pipeline.ExecuteAsync(async token => await action(token), cancellationToken);
            succeeded = true;
            return result;
        }
        catch (BrokenCircuitException exception)
        {
            throw new PaymentGatewayUnavailableException("Payment gateway circuit is open.", exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new PaymentGatewayUnavailableException("Payment gateway timed out.", exception);
        }
        finally
        {
            PaymentsTelemetry.RecordGatewayCall(operation, succeeded, stopwatch.Elapsed);
        }
    }
}
