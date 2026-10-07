using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Domain.Orders;
using OrderFlow.Orders.Infrastructure.Sagas;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceConstants.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        new OrderStateMap().Configure(modelBuilder);
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
