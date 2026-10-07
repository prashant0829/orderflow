namespace OrderFlow.Orders.Application.Abstractions;

public record OrderProgress(Guid OrderId, string State, string? FailureReason, string? PaymentTransactionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public interface IOrderProgressReader
{
    /// <summary>Where the order is in the stock -> payment -> confirmation flow. Null until the saga has started.</summary>
    Task<OrderProgress?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
