namespace OrderFlow.Contracts;

/// <summary>Queues that receive commands. Commands are sent to one queue; events are published to all subscribers.</summary>
public static class QueueNames
{
    public const string ReserveStock = "reserve-stock";
    public const string ReleaseStock = "release-stock";
    public const string ChargePayment = "charge-payment";
    public const string RefundPayment = "refund-payment";
}
