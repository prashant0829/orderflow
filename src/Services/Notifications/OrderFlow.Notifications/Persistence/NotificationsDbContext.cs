using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace OrderFlow.Notifications.Persistence;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string ConnectionStringName = "NotificationsDb";

    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();
    public DbSet<OrderTimelineEntry> TimelineEntries => Set<OrderTimelineEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        modelBuilder.Entity<SentNotification>(entity =>
        {
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Type).HasMaxLength(50).IsRequired();
            entity.Property(notification => notification.Channel).HasMaxLength(20).IsRequired();
            entity.Property(notification => notification.Recipient).HasMaxLength(200).IsRequired();
            entity.Property(notification => notification.Subject).HasMaxLength(200).IsRequired();
            entity.Property(notification => notification.Body).HasMaxLength(1000).IsRequired();

            // One notification of each type per order, even if the event is delivered twice.
            entity.HasIndex(notification => new { notification.OrderId, notification.Type }).IsUnique();
            entity.HasIndex(notification => new { notification.CustomerId, notification.CreatedAtUtc });
        });

        modelBuilder.Entity<OrderTimelineEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.State).HasMaxLength(50).IsRequired();
            entity.Property(entry => entry.Message).HasMaxLength(500).IsRequired();
            entity.HasIndex(entry => new { entry.OrderId, entry.State }).IsUnique();
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
