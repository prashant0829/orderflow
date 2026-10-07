using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.Application.Abstractions;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Newest first, across all customers. For admins only.</summary>
    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAllAsync(
        OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListByCustomerAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken);
}
