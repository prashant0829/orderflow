using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Application.Orders.Commands;

public record ConfirmOrderCommand(Guid OrderId) : IRequest;

public record FailOrderCommand(Guid OrderId, string Reason) : IRequest;

/// <summary>
/// Applied when the saga reports the result. If the customer already cancelled (the order is no longer pending)
/// the result is ignored: the saga has compensated or will compensate.
/// </summary>
public class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    ILogger<ConfirmOrderCommandHandler> logger) : IRequestHandler<ConfirmOrderCommand>
{
    public async Task Handle(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null || order.Status != OrderStatus.Pending)
        {
            logger.LogInformation("Ignoring confirmation for order {OrderId}: {Status}", command.OrderId, order?.Status.ToString() ?? "missing");
            return;
        }

        order.Confirm();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.Order(order.Id), cancellationToken);
    }
}

public class FailOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache,
    ILogger<FailOrderCommandHandler> logger) : IRequestHandler<FailOrderCommand>
{
    public async Task Handle(FailOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null || order.Status != OrderStatus.Pending)
        {
            logger.LogInformation("Ignoring failure for order {OrderId}: {Status}", command.OrderId, order?.Status.ToString() ?? "missing");
            return;
        }

        order.Fail(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.Order(order.Id), cancellationToken);
    }
}
