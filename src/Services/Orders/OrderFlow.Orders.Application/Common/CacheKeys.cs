namespace OrderFlow.Orders.Application.Common;

public static class CacheKeys
{
    public static string Order(Guid orderId) => $"orders:order:{orderId}";

    public static string Idempotency(Guid customerId, string idempotencyKey) => $"orders:idempotency:{customerId}:{idempotencyKey}";
}
