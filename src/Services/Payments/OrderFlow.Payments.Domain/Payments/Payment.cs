using OrderFlow.SharedKernel;

namespace OrderFlow.Payments.Domain.Payments;

public class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Payment Completed(Guid orderId, Guid customerId, decimal amount, string transactionId)
    {
        var payment = New(orderId, customerId, amount);
        payment.Status = PaymentStatus.Completed;
        payment.TransactionId = transactionId;
        return payment;
    }

    public static Payment Failed(Guid orderId, Guid customerId, decimal amount, string reason)
    {
        var payment = New(orderId, customerId, amount);
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;
        return payment;
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
        {
            throw new DomainException($"Only completed payments can be refunded. Current status: {Status}.");
        }

        Status = PaymentStatus.Refunded;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static Payment New(Guid orderId, Guid customerId, decimal amount)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order id is required.");
        }

        if (amount < 0)
        {
            throw new DomainException("Amount cannot be negative.");
        }

        var now = DateTime.UtcNow;
        return new Payment { Id = Guid.NewGuid(), OrderId = orderId, CustomerId = customerId, Amount = amount, CreatedAtUtc = now, UpdatedAtUtc = now };
    }
}
