using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Payments.Dtos;
using OrderFlow.Payments.Domain.Payments;
using OrderFlow.SharedKernel;

namespace OrderFlow.Payments.Application.Payments.Queries;

public record GetPaymentByOrderQuery(Guid OrderId) : IRequest<PaymentDto>;

public record ListRecentPaymentsQuery(int Count = 50) : IRequest<IReadOnlyList<PaymentDto>>;

public record GetCheckoutQuery(Guid OrderId) : IRequest<CheckoutDto>;

public class GetPaymentByOrderQueryHandler(IPaymentRepository repository) : IRequestHandler<GetPaymentByOrderQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByOrderQuery request, CancellationToken cancellationToken)
    {
        var payment = await repository.GetByOrderIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Payment for order", request.OrderId);
        return payment.ToDto();
    }
}

public class ListRecentPaymentsQueryHandler(IPaymentRepository repository)
    : IRequestHandler<ListRecentPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private const int MaxCount = 200;

    public async Task<IReadOnlyList<PaymentDto>> Handle(ListRecentPaymentsQuery request, CancellationToken cancellationToken)
    {
        var payments = await repository.ListRecentAsync(Math.Clamp(request.Count, 1, MaxCount), cancellationToken);
        return payments.Select(payment => payment.ToDto()).ToList();
    }
}

/// <summary>Everything the order page needs to show the card form. Secrets are only returned while the customer still has to pay.</summary>
public class GetCheckoutQueryHandler(IPaymentRepository repository, IPaymentGateway gateway) : IRequestHandler<GetCheckoutQuery, CheckoutDto>
{
    public async Task<CheckoutDto> Handle(GetCheckoutQuery request, CancellationToken cancellationToken)
    {
        var payment = await repository.GetByOrderIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Payment for order", request.OrderId);

        if (payment.Status != PaymentStatus.AwaitingCustomer || payment.ProviderPaymentId is null)
        {
            return new CheckoutDto(payment.Status.ToString(), payment.Amount, null, null, null, null);
        }

        var checkout = await gateway.GetCheckoutAsync(payment.ProviderPaymentId, cancellationToken);
        return new CheckoutDto(payment.Status.ToString(), payment.Amount, checkout?.Provider, checkout?.PublishableKey, checkout?.ClientSecret, checkout?.Currency);
    }
}
