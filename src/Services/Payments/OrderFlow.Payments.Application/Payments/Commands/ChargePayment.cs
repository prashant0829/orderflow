using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Commands;

public record ChargePaymentCommand(Guid OrderId, Guid CustomerId, decimal Amount) : IRequest<ChargePaymentResult>;

public record ChargePaymentResult(bool Success, string? TransactionId, string? FailureReason)
{
    public static ChargePaymentResult Succeeded(string transactionId) => new(true, transactionId, null);

    public static ChargePaymentResult Failed(string reason) => new(false, null, reason);
}

/// <summary>
/// One payment per order: if the command is delivered twice, the second run returns the first outcome
/// and never charges the customer again.
/// </summary>
public class ChargePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork) : IRequestHandler<ChargePaymentCommand, ChargePaymentResult>
{
    private const string GatewayUnavailableReason = "Payment gateway unavailable.";

    public async Task<ChargePaymentResult> Handle(ChargePaymentCommand command, CancellationToken cancellationToken)
    {
        var existing = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (existing is not null)
        {
            PaymentsTelemetry.RecordCharge(PaymentsTelemetry.Duplicate);
            return ToResult(existing);
        }

        var payment = await ChargeAsync(command, cancellationToken);

        await paymentRepository.AddAsync(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        PaymentsTelemetry.RecordCharge(payment.Status == PaymentStatus.Completed
            ? PaymentsTelemetry.Completed
            : payment.FailureReason == GatewayUnavailableReason ? PaymentsTelemetry.GatewayUnavailable : PaymentsTelemetry.Declined);
        return ToResult(payment);
    }

    private async Task<Payment> ChargeAsync(ChargePaymentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var charge = await gateway.ChargeAsync(command.OrderId, command.Amount, cancellationToken);

            return charge.Approved
                ? Payment.Completed(command.OrderId, command.CustomerId, command.Amount, charge.TransactionId!)
                : Payment.Failed(command.OrderId, command.CustomerId, command.Amount, charge.DeclineReason ?? "Payment declined.");
        }
        catch (PaymentGatewayUnavailableException)
        {
            // Retries and the circuit breaker already ran. Fail the payment so the order can be compensated.
            return Payment.Failed(command.OrderId, command.CustomerId, command.Amount, GatewayUnavailableReason);
        }
    }

    private static ChargePaymentResult ToResult(Payment payment) => payment.Status == PaymentStatus.Completed
        ? ChargePaymentResult.Succeeded(payment.TransactionId!)
        : ChargePaymentResult.Failed(payment.FailureReason ?? $"Payment is {payment.Status}.");
}
