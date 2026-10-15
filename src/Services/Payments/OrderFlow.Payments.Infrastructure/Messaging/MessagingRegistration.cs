using OrderFlow.Messaging;
using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Payments.Infrastructure.Persistence;

namespace OrderFlow.Payments.Infrastructure.Messaging;

public static class MessagingRegistration
{
    public static IBusRegistrationConfigurator AddPaymentsMessaging(this IBusRegistrationConfigurator registration)
    {
        registration.AddConsumer<ChargePaymentConsumer>().Endpoint(endpoint => endpoint.Name = QueueNames.ChargePayment);
        registration.AddConsumer<RefundPaymentConsumer>().Endpoint(endpoint => endpoint.Name = QueueNames.RefundPayment);

        registration.AddEntityFrameworkOutbox<PaymentsDbContext>(outbox =>
        {
            outbox.UseSqlServerWithOwnSchema<PaymentsDbContext>();

            // Webhooks and "check again" requests publish from an HTTP request, outside any consumer: the bus outbox covers that.
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.UseBusOutbox();
        });
        registration.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<PaymentsDbContext>(context));

        return registration;
    }
}
