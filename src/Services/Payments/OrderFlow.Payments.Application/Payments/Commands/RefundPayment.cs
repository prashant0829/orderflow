using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Commands;

public record RefundPaymentCommand(Guid OrderId) : IRequest<bool>;

/// <summary>Returns true when the order's payment is refunded (now or earlier); false when there was nothing to refund.</summary>
public class RefundPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork) : IRequestHandler<RefundPaymentCommand, bool>
{
    public async Task<bool> Handle(RefundPaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);

        if (payment is null || payment.Status == PaymentStatus.Failed)
        {
            return false;
        }

        if (payment.Status == PaymentStatus.Refunded)
        {
            return true;
        }

        await gateway.RefundAsync(payment.TransactionId!, cancellationToken);
        payment.Refund();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        PaymentsTelemetry.RecordRefund();
        return true;
    }
}
