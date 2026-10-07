using OrderFlow.Notifications.Persistence;

namespace OrderFlow.Notifications.Sending;

public record NotificationContent(string Type, string Subject, string Body);

public static class NotificationTemplates
{
    public static NotificationContent OrderReceived(Guid orderId, int itemCount, decimal total) => new(
        NotificationTypes.OrderReceived,
        $"We received your order {ShortId(orderId)}",
        $"Thanks for your order of {itemCount} item(s), total {total:0.00}. We are checking stock and processing payment.");

    public static NotificationContent OrderConfirmed(Guid orderId) => new(
        NotificationTypes.OrderConfirmed,
        $"Your order {ShortId(orderId)} is confirmed",
        "Your payment was received and your items are reserved for you.");

    public static NotificationContent OrderFailed(Guid orderId, string reason) => new(
        NotificationTypes.OrderFailed,
        $"We could not complete your order {ShortId(orderId)}",
        $"Sorry, your order failed: {reason} You have not been charged, or any charge is being refunded.");

    public static NotificationContent OrderCancelled(Guid orderId, string reason) => new(
        NotificationTypes.OrderCancelled,
        $"Your order {ShortId(orderId)} was cancelled",
        $"Your order was cancelled: {reason} Any payment is being refunded.");

    private static string ShortId(Guid orderId) => $"#{orderId.ToString("N")[..8].ToUpperInvariant()}";
}
