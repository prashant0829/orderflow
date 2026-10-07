using MediatR;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Payments.Dtos;
using OrderFlow.SharedKernel;

namespace OrderFlow.Payments.Application.Payments.Queries;

public record GetPaymentByOrderQuery(Guid OrderId) : IRequest<PaymentDto>;

public record ListRecentPaymentsQuery(int Count = 50) : IRequest<IReadOnlyList<PaymentDto>>;

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
