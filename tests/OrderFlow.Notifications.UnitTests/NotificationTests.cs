using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using OrderFlow.Contracts.Orders;
using OrderFlow.Notifications;
using OrderFlow.Notifications.Consumers;
using OrderFlow.Notifications.Persistence;
using OrderFlow.Notifications.Realtime;
using OrderFlow.Notifications.Sending;

namespace OrderFlow.Notifications.UnitTests;

public class NotificationTests
{
    private static readonly Guid OrderId = Guid.Parse("abcdef12-0000-0000-0000-000000000000");
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static NotificationsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public void Templates_UseShortOrderIdAndIncludeReason()
    {
        var failed = NotificationTemplates.OrderFailed(OrderId, "Card declined.");

        failed.Subject.Should().Contain("#ABCDEF12");
        failed.Body.Should().Contain("Card declined.");
        failed.Type.Should().Be(NotificationTypes.OrderFailed);
    }

    [Fact]
    public async Task Send_StoresNotificationAndEmails()
    {
        await using var db = NewDb();
        var email = Substitute.For<IEmailSender>();

        await new NotificationService(db, email).SendAsync(OrderId, CustomerId, NotificationTemplates.OrderConfirmed(OrderId), CancellationToken.None);

        (await db.SentNotifications.CountAsync()).Should().Be(1);
        await email.Received(1).SendAsync(Arg.Is<string>(to => to.Contains("customer-")), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_SameTypeTwice_EmailsOnlyOnce()
    {
        await using var db = NewDb();
        var email = Substitute.For<IEmailSender>();
        var service = new NotificationService(db, email);

        await service.SendAsync(OrderId, CustomerId, NotificationTemplates.OrderConfirmed(OrderId), CancellationToken.None);
        await service.SendAsync(OrderId, CustomerId, NotificationTemplates.OrderConfirmed(OrderId), CancellationToken.None);

        (await db.SentNotifications.CountAsync()).Should().Be(1);
        await email.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderSubmittedConsumer_PoisonCustomer_Throws()
    {
        var consumer = new OrderSubmittedConsumer(
            Substitute.For<INotificationService>(),
            Options.Create(new NotificationOptions { PoisonCustomerId = CustomerId }));

        var act = () => consumer.Consume(ContextFor(new OrderSubmitted(OrderId, CustomerId, [], 1m)));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StatusChangedConsumer_StoresTimelineOnceAndAlwaysPushes()
    {
        await using var db = NewDb();
        var notifier = Substitute.For<IOrderStatusNotifier>();
        var consumer = new OrderStatusChangedConsumer(db, notifier);
        var change = new OrderStatusChanged(OrderId, CustomerId, OrderProgressStates.AwaitingStock, "Reserving", DateTime.UtcNow);

        await consumer.Consume(ContextFor(change));
        await consumer.Consume(ContextFor(change));

        (await db.TimelineEntries.CountAsync()).Should().Be(1);
        await notifier.Received(2).PushAsync(change, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notifier_PushesToOrderAndCustomerGroups()
    {
        var hub = Substitute.For<IHubContext<OrderHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        var change = new OrderStatusChanged(OrderId, CustomerId, OrderProgressStates.Confirmed, "Done", DateTime.UtcNow);

        await new SignalROrderStatusNotifier(hub).PushAsync(change, CancellationToken.None);

        clients.Received(1).Groups(Arg.Is<IReadOnlyList<string>>(groups =>
            groups.Contains(RealtimeConstants.OrderGroup(OrderId)) && groups.Contains(RealtimeConstants.CustomerGroup(CustomerId))));
        await proxy.Received(1).SendCoreAsync(RealtimeConstants.OrderStatusChangedMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
