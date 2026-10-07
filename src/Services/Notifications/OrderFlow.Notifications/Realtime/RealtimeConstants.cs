namespace OrderFlow.Notifications.Realtime;

public static class RealtimeConstants
{
    public const string HubPath = "/hubs/orders";
    public const string OrderStatusChangedMethod = "orderStatusChanged";

    public static string OrderGroup(Guid orderId) => $"order:{orderId}";

    public static string CustomerGroup(Guid customerId) => $"customer:{customerId}";
}
