using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderFlow.Messaging.DeadLetters;

/// <summary>
/// Lists and peeks error queues through the RabbitMQ management API, and replays messages with the
/// AMQP client: a message is only acknowledged (removed) after the republish is confirmed by the broker.
/// </summary>
public class RabbitMqDeadLetterService(
    IHttpClientFactory httpClientFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqDeadLetterService> logger) : IDeadLetterService
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<IReadOnlyList<DeadLetterQueueInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(DeadLetterConstants.ManagementHttpClientName);
        var vhost = Uri.EscapeDataString(_options.VirtualHost);

        using var document = await client.GetFromJsonAsync<JsonDocument>(new Uri($"api/queues/{vhost}", UriKind.Relative), cancellationToken)
            ?? throw new InvalidOperationException("RabbitMQ management API returned no content.");

        return document.RootElement.EnumerateArray()
            .Select(queue => (Name: queue.GetProperty("name").GetString()!, Count: queue.GetProperty("messages").GetInt32()))
            .Where(queue => queue.Name.EndsWith(DeadLetterConstants.ErrorQueueSuffix, StringComparison.Ordinal))
            .Select(queue => new DeadLetterQueueInfo(queue.Name, OriginalQueueName(queue.Name), queue.Count))
            .OrderBy(queue => queue.Queue, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<DeadLetterMessage>> PeekAsync(string errorQueue, int count, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(DeadLetterConstants.ManagementHttpClientName);
        var vhost = Uri.EscapeDataString(_options.VirtualHost);
        var body = new { count = Math.Clamp(count, 1, DeadLetterConstants.MaxPeekCount), ackmode = "ack_requeue_true", encoding = "auto" };

        using var response = await client.PostAsJsonAsync(
            new Uri($"api/queues/{vhost}/{Uri.EscapeDataString(errorQueue)}/get", UriKind.Relative), body, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        return document.RootElement.EnumerateArray().Select(ToDeadLetterMessage).ToList();
    }

    public async Task<ReplayResult> ReplayAsync(string errorQueue, CancellationToken cancellationToken)
    {
        var originalQueue = OriginalQueueName(errorQueue);
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.Username,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        var pending = (int)(await channel.QueueDeclarePassiveAsync(errorQueue, cancellationToken)).MessageCount;
        var replayed = 0;

        while (replayed < pending && !cancellationToken.IsCancellationRequested)
        {
            var delivery = await channel.BasicGetAsync(errorQueue, autoAck: false, cancellationToken);
            if (delivery is null)
            {
                break;
            }

            var properties = new BasicProperties
            {
                ContentType = delivery.BasicProperties.ContentType,
                MessageId = delivery.BasicProperties.MessageId,
                CorrelationId = delivery.BasicProperties.CorrelationId,
                DeliveryMode = DeliveryModes.Persistent,
                Headers = delivery.BasicProperties.Headers?
                    .Where(header => !IsFaultHeader(header.Key))
                    .ToDictionary(header => header.Key, header => header.Value)
            };

            // MassTransit receive endpoints bind a fanout exchange with the same name as the queue.
            // With publisher confirms tracking on, this call only returns once the broker has accepted the message.
            await channel.BasicPublishAsync(originalQueue, string.Empty, mandatory: false, properties, delivery.Body, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            replayed++;
        }

        logger.LogInformation("Replayed {Count} message(s) from {ErrorQueue} to {OriginalQueue}", replayed, errorQueue, originalQueue);
        return new ReplayResult(errorQueue, replayed);
    }

    private static DeadLetterMessage ToDeadLetterMessage(JsonElement message)
    {
        var properties = message.GetProperty("properties");
        var headers = properties.TryGetProperty("headers", out var headersElement) ? headersElement : default;

        return new DeadLetterMessage(
            properties.TryGetProperty("message_id", out var messageId) ? messageId.GetString() : null,
            message.GetProperty("payload").GetString() ?? string.Empty,
            ReadHeader(headers, DeadLetterConstants.FaultExceptionTypeHeader),
            ReadHeader(headers, DeadLetterConstants.FaultMessageHeader),
            ReadHeader(headers, DeadLetterConstants.FaultTimestampHeader));
    }

    private static string? ReadHeader(JsonElement headers, string name) =>
        headers.ValueKind == JsonValueKind.Object && headers.TryGetProperty(name, out var value) ? value.ToString() : null;

    private static bool IsFaultHeader(string name) =>
        name.StartsWith(DeadLetterConstants.FaultHeaderPrefix, StringComparison.Ordinal) || name == DeadLetterConstants.ReasonHeader;

    private static string OriginalQueueName(string errorQueue) => errorQueue[..^DeadLetterConstants.ErrorQueueSuffix.Length];
}
