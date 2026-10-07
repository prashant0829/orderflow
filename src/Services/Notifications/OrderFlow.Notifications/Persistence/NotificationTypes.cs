namespace OrderFlow.Notifications.Persistence;

public static class NotificationTypes
{
    public const string OrderReceived = "OrderReceived";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderFailed = "OrderFailed";
    public const string OrderCancelled = "OrderCancelled";
}

public static class NotificationChannels
{
    public const string Email = "Email";
}
