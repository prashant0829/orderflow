namespace OrderFlow.Payments.Application.Abstractions;

/// <summary>Publishes an event to the other services. The message is stored with the database changes (outbox), so both happen or neither does.</summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class;
}
