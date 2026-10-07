using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await dbContext.Orders.AddAsync(order, cancellationToken);

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.Include(order => order.Lines).FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.Orders.Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.CustomerId == customerId && order.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAllAsync(
        OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Orders.AsNoTracking().AsQueryable();
        if (status is { } wanted)
        {
            query = query.Where(order => order.Status == wanted);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(order => order.Lines)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Orders.AsNoTracking().Where(order => order.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(order => order.Lines)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
