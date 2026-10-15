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

    /// <summary>The provider's id for this payment (for Stripe, the PaymentIntent "pi_..."). Used to cancel, refund and look it up from webhooks.</summary>
    public string? ProviderPaymentId { get; private set; }

    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Stops a webhook and a "check again" request from changing the same payment at the same moment.</summary>
    public byte[] RowVersion { get; private set; } = [];

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

    public static Payment AwaitingCustomer(Guid orderId, Guid customerId, decimal amount, string providerPaymentId)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            throw new DomainException("A provider payment id is required.");
        }

        var payment = New(orderId, customerId, amount);
        payment.Status = PaymentStatus.AwaitingCustomer;
        payment.ProviderPaymentId = providerPaymentId;
        return payment;
    }

    /// <summary>The provider confirmed the money was taken. Safe to call again.</summary>
    public void Complete(string transactionId)
    {
        if (Status == PaymentStatus.Completed)
        {
            return;
        }

        if (Status != PaymentStatus.AwaitingCustomer)
        {
            throw new DomainException($"Only a payment waiting for the customer can be completed. Current status: {Status}.");
        }

        Status = PaymentStatus.Completed;
        TransactionId = transactionId;
        Touch();
    }

    /// <summary>The provider payment was cancelled before any money was taken. Safe to call again.</summary>
    public void Cancel()
    {
        if (Status == PaymentStatus.Cancelled)
        {
            return;
        }

        if (Status != PaymentStatus.AwaitingCustomer)
        {
            throw new DomainException($"Only a payment waiting for the customer can be cancelled. Current status: {Status}.");
        }

        Status = PaymentStatus.Cancelled;
        Touch();
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
        {
            throw new DomainException($"Only completed payments can be refunded. Current status: {Status}.");
        }

        Status = PaymentStatus.Refunded;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

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
