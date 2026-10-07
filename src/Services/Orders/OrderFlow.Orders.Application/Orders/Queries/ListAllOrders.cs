using FluentValidation;
using MediatR;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Application.Orders.Queries;

/// <summary>Admin view of every order. Status is optional ("Failed", "Pending", ...).</summary>
public record ListAllOrdersQuery(string? Status, int Page = 1, int PageSize = PagingLimits.DefaultPageSize)
    : IRequest<PagedResult<OrderDto>>;

public class ListAllOrdersQueryValidator : AbstractValidator<ListAllOrdersQuery>
{
    public ListAllOrdersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PagingLimits.MaxPageSize);
        RuleFor(query => query.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || Enum.TryParse<OrderStatus>(status, ignoreCase: true, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
    }
}

public class ListAllOrdersQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<ListAllOrdersQuery, PagedResult<OrderDto>>
{
    public async Task<PagedResult<OrderDto>> Handle(ListAllOrdersQuery query, CancellationToken cancellationToken)
    {
        OrderStatus? status = Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var parsed) ? parsed : null;

        var (orders, totalCount) = await orderRepository.ListAllAsync(status, query.Page, query.PageSize, cancellationToken);

        return new PagedResult<OrderDto>(orders.Select(order => order.ToDto()).ToList(), query.Page, query.PageSize, totalCount);
    }
}
