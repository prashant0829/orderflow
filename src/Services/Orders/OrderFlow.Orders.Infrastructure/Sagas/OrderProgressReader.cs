using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Infrastructure.Persistence;

namespace OrderFlow.Orders.Infrastructure.Sagas;

public class OrderProgressReader(OrdersDbContext dbContext) : IOrderProgressReader
{
    public Task<OrderProgress?> GetAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Set<OrderState>().AsNoTracking()
            .Where(saga => saga.CorrelationId == orderId)
            .Select(saga => new OrderProgress(saga.CorrelationId, saga.CurrentState, saga.FailureReason, saga.PaymentTransactionId, saga.CreatedAtUtc, saga.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
