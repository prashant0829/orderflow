using FluentValidation;
using MediatR;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;

namespace OrderFlow.Orders.Application.Orders.Queries;

public record ListOrdersQuery(Guid CustomerId, int Page = 1, int PageSize = PagingLimits.DefaultPageSize)
    : IRequest<PagedResult<OrderDto>>;

public class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.CustomerId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PagingLimits.MaxPageSize);
    }
}

public class ListOrdersQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<ListOrdersQuery, PagedResult<OrderDto>>
{
    public async Task<PagedResult<OrderDto>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        var (orders, totalCount) = await orderRepository.ListByCustomerAsync(
            query.CustomerId, query.Page, query.PageSize, cancellationToken);

        return new PagedResult<OrderDto>(orders.Select(order => order.ToDto()).ToList(), query.Page, query.PageSize, totalCount);
    }
}
