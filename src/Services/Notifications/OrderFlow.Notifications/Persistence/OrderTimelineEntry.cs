namespace OrderFlow.Notifications.Persistence;

/// <summary>One step in an order's life, kept so a page opened late can still show the full history.</summary>
public class OrderTimelineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
