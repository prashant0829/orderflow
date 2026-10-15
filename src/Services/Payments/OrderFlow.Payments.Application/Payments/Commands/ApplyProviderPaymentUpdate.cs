using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.Payments;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Payments.Dtos;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Payments.Commands;

/// <summary>The provider tells us what happened to a payment (by webhook, or because we asked).</summary>
public record ApplyProviderPaymentUpdateCommand(string ProviderPaymentId, ProviderPaymentOutcome Outcome) : IRequest<PaymentDto?>;

/// <summary>
/// Safe to run any number of times and in any order: a webhook and a "check again" request can both arrive for the same payment.
/// A failed card attempt is not final (the customer can try another card); the order ends by timeout or cancellation instead.
/// </summary>
public class ApplyProviderPaymentUpdateCommandHandler(
    IPaymentRepository paymentRepository,
    IIntegrationEventPublisher publisher,
    IUnitOfWork unitOfWork,
    ILogger<ApplyProviderPaymentUpdateCommandHandler> logger) : IRequestHandler<ApplyProviderPaymentUpdateCommand, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(ApplyProviderPaymentUpdateCommand command, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByProviderPaymentIdAsync(command.ProviderPaymentId, cancellationToken);
        if (payment is null)
        {
            // An event for a payment we do not know (another app on the same provider account, or a test event).
            logger.LogInformation("Ignoring {Outcome} for unknown provider payment {ProviderPaymentId}", command.Outcome, command.ProviderPaymentId);
            return null;
        }

        if (payment.Status != PaymentStatus.AwaitingCustomer)
        {
            return payment.ToDto();
        }

        switch (command.Outcome)
        {
            case ProviderPaymentOutcome.Succeeded:
                payment.Complete(command.ProviderPaymentId);
                await publisher.PublishAsync(new PaymentCompleted(payment.OrderId, command.ProviderPaymentId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                break;

            case ProviderPaymentOutcome.Canceled:
                payment.Cancel();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                break;

            case ProviderPaymentOutcome.Failed:
                logger.LogInformation("Card attempt failed for order {OrderId}; the customer can try again", payment.OrderId);
                break;
        }

        return payment.ToDto();
    }
}

/// <summary>Asks the provider where the order's payment stands and applies it. Used by the order page right after the customer pays.</summary>
public record RefreshPaymentCommand(Guid OrderId) : IRequest<PaymentDto?>;

public class RefreshPaymentCommandHandler(IPaymentRepository paymentRepository, IPaymentGateway gateway, ISender sender)
    : IRequestHandler<RefreshPaymentCommand, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(RefreshPaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (payment is not { Status: PaymentStatus.AwaitingCustomer, ProviderPaymentId: { } providerPaymentId })
        {
            return payment?.ToDto();
        }

        var outcome = await gateway.GetStatusAsync(providerPaymentId, cancellationToken) switch
        {
            ProviderPaymentState.Succeeded => ProviderPaymentOutcome.Succeeded,
            ProviderPaymentState.Canceled => ProviderPaymentOutcome.Canceled,
            _ => ProviderPaymentOutcome.Failed // still waiting: nothing to apply
        };

        return await sender.Send(new ApplyProviderPaymentUpdateCommand(providerPaymentId, outcome), cancellationToken);
    }
}
