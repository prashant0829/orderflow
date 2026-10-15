using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Commands;

public record RefundPaymentCommand(Guid OrderId) : IRequest<bool>;

/// <summary>
/// Undoes the payment of an order that ended. Money already taken is refunded; a payment the customer never completed is cancelled.
/// Returns true when there is nothing left to undo (done now or earlier); false when there was nothing, or when it cannot be undone yet
/// (the customer paid at this very moment: the payment-completed event will arrive and trigger the refund).
/// </summary>
public class RefundPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork) : IRequestHandler<RefundPaymentCommand, bool>
{
    public async Task<bool> Handle(RefundPaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);

        switch (payment?.Status)
        {
            case null or PaymentStatus.Failed:
                return false;

            case PaymentStatus.Refunded or PaymentStatus.Cancelled:
                return true;

            case PaymentStatus.AwaitingCustomer:
                return await CancelUnpaidAsync(payment, cancellationToken);

            default:
                await gateway.RefundAsync(payment.TransactionId!, cancellationToken);
                payment.Refund();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                PaymentsTelemetry.RecordRefund();
                return true;
        }
    }

    private async Task<bool> CancelUnpaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!await gateway.CancelAsync(payment.ProviderPaymentId!, cancellationToken))
        {
            return false;
        }

        payment.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
