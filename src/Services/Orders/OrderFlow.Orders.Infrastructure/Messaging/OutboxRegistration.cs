using OrderFlow.Messaging;
using MassTransit;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Sagas;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public static class OutboxRegistration
{
    private const int OutboxPollSeconds = 1;

    public static IBusRegistrationConfigurator AddOrdersMessaging(this IBusRegistrationConfigurator registration)
    {
        registration.AddDelayedMessageScheduler();

        registration.AddConsumer<OrderConfirmedConsumer>();
        registration.AddConsumer<OrderFailedConsumer>();

        registration.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .EntityFrameworkRepository(repository =>
            {
                repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
                repository.ExistingDbContext<OrdersDbContext>();
            });

        registration.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
        {
            outbox.UseSqlServerWithOwnSchema<OrdersDbContext>();
            outbox.QueryDelay = TimeSpan.FromSeconds(OutboxPollSeconds);
            outbox.UseBusOutbox();
        });

        // Consumers and the saga: inbox (no duplicates) + outbox (state change and messages commit together).
        registration.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<OrdersDbContext>(context));

        return registration;
    }
}
