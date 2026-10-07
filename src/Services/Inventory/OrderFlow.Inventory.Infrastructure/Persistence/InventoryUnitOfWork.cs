using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.Infrastructure.Persistence;

public class InventoryUnitOfWork(InventoryDbContext dbContext) : IUnitOfWork
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("The stock was changed by another request. Try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation })
        {
            throw new ConflictException("A reservation for this order already exists.", exception);
        }
    }
}
