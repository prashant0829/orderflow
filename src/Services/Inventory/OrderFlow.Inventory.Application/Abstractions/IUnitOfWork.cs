namespace OrderFlow.Inventory.Application.Abstractions;

public interface IUnitOfWork
{
    /// <exception cref="OrderFlow.SharedKernel.ConflictException">Another request changed the same rows first.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
