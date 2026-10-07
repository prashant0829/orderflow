using OrderFlow.Messaging;
using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public static class MessagingRegistration
{
    public static IBusRegistrationConfigurator AddInventoryMessaging(this IBusRegistrationConfigurator registration)
    {
        registration.AddConsumer<ReserveStockConsumer>().Endpoint(endpoint => endpoint.Name = QueueNames.ReserveStock);
        registration.AddConsumer<ReleaseStockConsumer>().Endpoint(endpoint => endpoint.Name = QueueNames.ReleaseStock);

        // Inbox + outbox on every endpoint: DB changes and the events we publish commit together, duplicates are skipped.
        registration.AddEntityFrameworkOutbox<InventoryDbContext>(outbox => outbox.UseSqlServerWithOwnSchema<InventoryDbContext>());
        registration.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<InventoryDbContext>(context));

        return registration;
    }
}
