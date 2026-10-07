using MediatR;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Queries;

public record GetOrderProgressQuery(Guid OrderId) : IRequest<OrderProgress>;

public class GetOrderProgressQueryHandler(IOrderProgressReader progressReader, IOrderRepository orderRepository)
    : IRequestHandler<GetOrderProgressQuery, OrderProgress>
{
    private const string SubmittedState = "Submitted";

    public async Task<OrderProgress> Handle(GetOrderProgressQuery request, CancellationToken cancellationToken)
    {
        var progress = await progressReader.GetAsync(request.OrderId, cancellationToken);
        if (progress is not null)
        {
            return progress;
        }

        // The saga starts a moment after the order is saved (outbox delivery), so a brand-new order has no saga row yet.
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        return new OrderProgress(order.Id, SubmittedState, null, null, order.CreatedAtUtc, order.UpdatedAtUtc);
    }
}
