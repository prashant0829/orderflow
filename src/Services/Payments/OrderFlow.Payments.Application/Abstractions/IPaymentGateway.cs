namespace OrderFlow.Payments.Application.Abstractions;

public record GatewayChargeResult(bool Approved, string? TransactionId, string? DeclineReason)
{
    public static GatewayChargeResult Approve(string transactionId) => new(true, transactionId, null);

    public static GatewayChargeResult Decline(string reason) => new(false, null, reason);
}

public interface IPaymentGateway
{
    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task<GatewayChargeResult> ChargeAsync(Guid orderId, decimal amount, CancellationToken cancellationToken);

    /// <exception cref="Common.PaymentGatewayUnavailableException">The gateway could not be reached (temporary).</exception>
    Task RefundAsync(string transactionId, CancellationToken cancellationToken);
}
