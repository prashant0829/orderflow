using OrderFlow.SharedKernel;
using MediatR;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;

namespace OrderFlow.Orders.Application.Orders.Queries;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto>;

public class GetOrderQueryHandler(IOrderRepository orderRepository, ICacheService cache)
    : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.Order(query.OrderId);

        var cached = await cache.GetAsync<OrderDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);

        var dto = order.ToDto();
        await cache.SetAsync(cacheKey, dto, CacheDurations.Order, cancellationToken);
        return dto;
    }
}
