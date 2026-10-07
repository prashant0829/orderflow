using OrderFlow.SharedKernel;
using FluentValidation;
using MediatR;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;

namespace OrderFlow.Orders.Application.Orders.Commands;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<OrderDto>;

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    private const int MaxReasonLength = 500;

    public CancelOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(MaxReasonLength);
    }
}

public class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : IRequestHandler<CancelOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.Cancel(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.Order(order.Id), cancellationToken);

        return order.ToDto();
    }
}
