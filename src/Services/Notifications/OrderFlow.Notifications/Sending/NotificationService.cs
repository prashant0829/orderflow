using Microsoft.EntityFrameworkCore;
using OrderFlow.Notifications.Persistence;

namespace OrderFlow.Notifications.Sending;

public interface INotificationService
{
    Task SendAsync(Guid orderId, Guid customerId, NotificationContent content, CancellationToken cancellationToken);
}

/// <summary>Records the notification and sends the email. A repeated event for the same order and type is skipped.</summary>
public class NotificationService(NotificationsDbContext dbContext, IEmailSender emailSender) : INotificationService
{
    public async Task SendAsync(Guid orderId, Guid customerId, NotificationContent content, CancellationToken cancellationToken)
    {
        var alreadySent = await dbContext.SentNotifications
            .AnyAsync(notification => notification.OrderId == orderId && notification.Type == content.Type, cancellationToken);
        if (alreadySent)
        {
            return;
        }

        var recipient = $"customer-{customerId:N}@orderflow.local";

        await emailSender.SendAsync(recipient, content.Subject, content.Body, cancellationToken);

        dbContext.SentNotifications.Add(new SentNotification
        {
            OrderId = orderId,
            CustomerId = customerId,
            Type = content.Type,
            Channel = NotificationChannels.Email,
            Recipient = recipient,
            Subject = content.Subject,
            Body = content.Body
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        NotificationsTelemetry.RecordSent(content.Type);
    }
}
