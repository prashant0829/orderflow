using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Application.Abstractions;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Payment>> ListRecentAsync(int count, CancellationToken cancellationToken);
}
