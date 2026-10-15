using MassTransit;
using MediatR;
using OrderFlow.Contracts.Payments;
using OrderFlow.Payments.Application.Payments.Commands;

namespace OrderFlow.Payments.Infrastructure.Messaging;

public class ChargePaymentConsumer(ISender sender) : IConsumer<ChargePayment>
{
    public async Task Consume(ConsumeContext<ChargePayment> context)
    {
        var message = context.Message;
        var result = await sender.Send(new ChargePaymentCommand(message.OrderId, message.CustomerId, message.Amount), context.CancellationToken);

        switch (result.Kind)
        {
            case ChargeResultKind.Succeeded:
                await context.Publish(new PaymentCompleted(message.OrderId, result.TransactionId!));
                break;

            case ChargeResultKind.Failed:
                await context.Publish(new PaymentFailed(message.OrderId, result.FailureReason ?? "Unknown reason."));
                break;

            // Pending: the customer still has to pay. Stripe's webhook (or a "check again") announces the result later.
        }
    }
}

public class RefundPaymentConsumer(ISender sender) : IConsumer<RefundPayment>
{
    public async Task Consume(ConsumeContext<RefundPayment> context)
    {
        var refunded = await sender.Send(new RefundPaymentCommand(context.Message.OrderId), context.CancellationToken);

        if (refunded)
        {
            await context.Publish(new PaymentRefunded(context.Message.OrderId));
        }
    }
}
