using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Common;
using OrderFlow.Orders.Infrastructure.Messaging;

namespace OrderFlow.Orders.Infrastructure.Persistence;

/// <summary>
/// Publishes integration events through the MassTransit bus outbox: they are written to the
/// OutboxMessage table in the same SQL transaction as the order, then delivered to RabbitMQ afterwards.
/// </summary>
public class OrdersUnitOfWork(OrdersDbContext dbContext, IPublishEndpoint publishEndpoint) : IUnitOfWork
{
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await PublishDomainEventsAsync(cancellationToken);

        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new DuplicateRequestException("A request with the same idempotency key already exists.", exception);
        }
    }

    private async Task PublishDomainEventsAsync(CancellationToken cancellationToken)
    {
        var aggregates = dbContext.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var integrationEvent = IntegrationEventMapper.ToIntegrationEvent(domainEvent);
                if (integrationEvent is not null)
                {
                    await publishEndpoint.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
                }
            }

            aggregate.ClearDomainEvents();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation };
}
