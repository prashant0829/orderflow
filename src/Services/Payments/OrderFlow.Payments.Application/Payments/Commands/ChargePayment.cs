using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Commands;

public record ChargePaymentCommand(Guid OrderId, Guid CustomerId, decimal Amount) : IRequest<ChargePaymentResult>;

public enum ChargeResultKind
{
    Succeeded,
    Failed,

    /// <summary>The customer still has to pay. Nothing is announced yet; the result comes later.</summary>
    Pending
}

public record ChargePaymentResult(ChargeResultKind Kind, string? TransactionId, string? FailureReason)
{
    public bool Success => Kind == ChargeResultKind.Succeeded;

    public static ChargePaymentResult Succeeded(string transactionId) => new(ChargeResultKind.Succeeded, transactionId, null);

    public static ChargePaymentResult Failed(string reason) => new(ChargeResultKind.Failed, null, reason);

    public static ChargePaymentResult Pending() => new(ChargeResultKind.Pending, null, null);
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

        PaymentsTelemetry.RecordCharge(payment.Status switch
        {
            PaymentStatus.Completed => PaymentsTelemetry.Completed,
            PaymentStatus.AwaitingCustomer => PaymentsTelemetry.AwaitingCustomer,
            _ => payment.FailureReason == GatewayUnavailableReason ? PaymentsTelemetry.GatewayUnavailable : PaymentsTelemetry.Declined
        });
        return ToResult(payment);
    }

    private async Task<Payment> ChargeAsync(ChargePaymentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var charge = await gateway.ChargeAsync(command.OrderId, command.Amount, cancellationToken);

            return charge.Outcome switch
            {
                ChargeOutcome.Approved => Payment.Completed(command.OrderId, command.CustomerId, command.Amount, charge.TransactionId!),
                ChargeOutcome.ActionRequired => Payment.AwaitingCustomer(command.OrderId, command.CustomerId, command.Amount, charge.TransactionId!),
                _ => Payment.Failed(command.OrderId, command.CustomerId, command.Amount, charge.DeclineReason ?? "Payment declined.")
            };
        }
        catch (PaymentGatewayUnavailableException)
        {
            // Retries and the circuit breaker already ran. Fail the payment so the order can be compensated.
            return Payment.Failed(command.OrderId, command.CustomerId, command.Amount, GatewayUnavailableReason);
        }
    }

    private static ChargePaymentResult ToResult(Payment payment) => payment.Status switch
    {
        PaymentStatus.Completed => ChargePaymentResult.Succeeded(payment.TransactionId!),
        PaymentStatus.AwaitingCustomer => ChargePaymentResult.Pending(),
        _ => ChargePaymentResult.Failed(payment.FailureReason ?? $"Payment is {payment.Status}.")
    };
}
