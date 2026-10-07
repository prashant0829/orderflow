using Microsoft.EntityFrameworkCore;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.Infrastructure.Persistence;

public class PaymentRepository(PaymentsDbContext dbContext) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        await dbContext.Payments.AddAsync(payment, cancellationToken);

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Payments.FirstOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListRecentAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.Payments.AsNoTracking().OrderByDescending(payment => payment.CreatedAtUtc).Take(count).ToListAsync(cancellationToken);
}
