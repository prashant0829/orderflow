using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddOrderFlowMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configureRegistrations = null,
        Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext>? configureEndpoints = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(registration =>
        {
            // Prefix with the service name so two services can never share (and steal from) the same queue.
            registration.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceName, includeNamespace: false));
            configureRegistrations?.Invoke(registration);

            registration.UsingRabbitMq((context, bus) =>
            {
                bus.Host(options.Host, options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });

                bus.UseMessageRetry(retry => retry.Exponential(
                    options.RetryCount,
                    TimeSpan.FromMilliseconds(options.RetryInitialIntervalMs),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMilliseconds(options.RetryInitialIntervalMs)));

                configureEndpoints?.Invoke(bus, context);
                bus.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
