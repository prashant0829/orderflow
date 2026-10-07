using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

namespace OrderFlow.Messaging;

public static class OutboxExtensions
{
    /// <summary>
    /// SQL Server outbox/inbox that always uses the schema of its own DbContext.
    /// MassTransit caches the schema per entity type (InboxState is the same type in every service), so with the default
    /// setting the first service to run in a process decides the schema for all the others. Separate processes never notice;
    /// anything that hosts several services together (integration tests, a modular monolith) would.
    /// </summary>
    public static void UseSqlServerWithOwnSchema<TDbContext>(this IEntityFrameworkOutboxConfigurator configurator)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        configurator.UseSqlServer();
        configurator.LockStatementProvider = new SqlServerLockStatementProvider(enableSchemaCaching: false);
    }
}
