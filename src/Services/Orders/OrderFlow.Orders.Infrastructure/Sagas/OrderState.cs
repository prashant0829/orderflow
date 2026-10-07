using MassTransit;

namespace OrderFlow.Orders.Infrastructure.Sagas;

/// <summary>One row per order: where it is in the flow stock -> payment -> confirmed. Survives restarts because it lives in SQL.</summary>
public class OrderState : SagaStateMachineInstance
{
    /// <summary>Same value as the order id.</summary>
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? FailureReason { get; set; }
    public string? PaymentTransactionId { get; set; }
    public Guid? PaymentTimeoutTokenId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
