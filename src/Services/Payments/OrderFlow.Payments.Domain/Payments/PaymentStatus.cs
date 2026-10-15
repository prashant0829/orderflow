namespace OrderFlow.Payments.Domain.Payments;

public enum PaymentStatus
{
    Completed = 1,
    Failed = 2,
    Refunded = 3,

    /// <summary>A payment exists at the provider and the customer still has to pay (enter card details).</summary>
    AwaitingCustomer = 4,

    /// <summary>We cancelled the provider payment before the customer paid (order cancelled or timed out).</summary>
    Cancelled = 5
}
