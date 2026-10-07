namespace OrderFlow.Payments.Application.Abstractions;

public interface IUnitOfWork
{
    /// <exception cref="OrderFlow.SharedKernel.ConflictException">A payment for this order was saved by a parallel request.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
