using MassTransit;
using OrderFlow.Payments.Application.Abstractions;

namespace OrderFlow.Payments.Infrastructure.Messaging;

/// <summary>Inside an HTTP request this goes through the bus outbox, so the event is saved together with the payment change.</summary>
public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class =>
        publishEndpoint.Publish(message, cancellationToken);
}
