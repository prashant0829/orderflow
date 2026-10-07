using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Payments.Infrastructure.Persistence;

public class PaymentsUnitOfWork(PaymentsDbContext dbContext) : IUnitOfWork
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation })
        {
            throw new ConflictException("A payment for this order already exists.", exception);
        }
    }
}
