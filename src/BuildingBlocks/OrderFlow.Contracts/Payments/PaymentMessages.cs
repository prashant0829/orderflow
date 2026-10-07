namespace OrderFlow.Contracts.Payments;

public record ChargePayment(Guid OrderId, Guid CustomerId, decimal Amount);

public record PaymentCompleted(Guid OrderId, string TransactionId);

public record PaymentFailed(Guid OrderId, string Reason);

public record RefundPayment(Guid OrderId, string Reason);

public record PaymentRefunded(Guid OrderId);
