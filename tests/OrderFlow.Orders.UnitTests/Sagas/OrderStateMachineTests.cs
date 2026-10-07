using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Contracts.Inventory;
using OrderFlow.Contracts.Orders;
using OrderFlow.Contracts.Payments;
using OrderFlow.Orders.Infrastructure.Sagas;

namespace OrderFlow.Orders.UnitTests.Sagas;

public sealed class OrderStateMachineTests : IAsyncDisposable
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly OrderSubmitted _submitted;
    private ServiceProvider? _provider;
    private ITestHarness _harness = null!;

    public OrderStateMachineTests() =>
        _submitted = new OrderSubmitted(_orderId, CustomerId, [new OrderLine(Guid.NewGuid(), 2, 10m)], 20m);

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    private async Task<ISagaStateMachineTestHarness<OrderStateMachine, OrderState>> StartAsync(int paymentTimeoutSeconds = 60)
    {
        _provider = new ServiceCollection()
            .Configure<OrderSagaOptions>(options => options.PaymentTimeoutSeconds = paymentTimeoutSeconds)
            .AddMassTransitTestHarness(configurator =>
            {
                configurator.AddDelayedMessageScheduler();
                configurator.AddSagaStateMachine<OrderStateMachine, OrderState>();
                configurator.UsingInMemory((context, bus) =>
                {
                    bus.UseDelayedMessageScheduler();
                    bus.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        return _harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
    }

    private Task<bool> IsInState(ISagaStateMachineTestHarness<OrderStateMachine, OrderState> saga, Func<OrderStateMachine, State> state) =>
        saga.Exists(_orderId, state, TimeSpan.FromSeconds(5)).ContinueWith(task => task.Result.HasValue);

    private Task<bool> Publish<T>(T message) where T : class => _harness.Bus.Publish(message).ContinueWith(_ => true);

    [Fact]
    public async Task Submitted_AsksInventoryToReserveStock()
    {
        var saga = await StartAsync();

        await Publish(_submitted);

        (await IsInState(saga, m => m.AwaitingStock)).Should().BeTrue();
        (await _harness.Sent.Any<ReserveStock>(x => x.Context.Message.OrderId == _orderId)).Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateSubmitted_ReservesStockOnlyOnce()
    {
        var saga = await StartAsync();

        await Publish(_submitted);
        await IsInState(saga, m => m.AwaitingStock);
        await Publish(_submitted);
        await Task.Delay(300);

        _harness.Sent.Select<ReserveStock>().Count().Should().Be(1);
    }

    [Fact]
    public async Task StockReserved_AsksPaymentsToChargeTheOrderTotal()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await IsInState(saga, m => m.AwaitingStock);

        await Publish(new StockReserved(_orderId));

        (await IsInState(saga, m => m.AwaitingPayment)).Should().BeTrue();
        (await _harness.Sent.Any<ChargePayment>(x => x.Context.Message.Amount == 20m && x.Context.Message.CustomerId == CustomerId)).Should().BeTrue();
    }

    [Fact]
    public async Task HappyPath_EndsConfirmedAndAnnouncesIt()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));
        await IsInState(saga, m => m.AwaitingPayment);

        await Publish(new PaymentCompleted(_orderId, "txn_1"));

        (await IsInState(saga, m => m.Confirmed)).Should().BeTrue();
        (await _harness.Published.Any<OrderConfirmed>()).Should().BeTrue();
        _harness.Sent.Select<ReleaseStock>().Any().Should().BeFalse();
    }

    [Fact]
    public async Task StockUnavailable_FailsOrderWithoutCharging()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await IsInState(saga, m => m.AwaitingStock);

        await Publish(new StockReservationFailed(_orderId, "Insufficient stock"));

        (await IsInState(saga, m => m.Failed)).Should().BeTrue();
        (await _harness.Published.Any<OrderFailed>(x => x.Context.Message.Reason == "Insufficient stock")).Should().BeTrue();
        _harness.Sent.Select<ChargePayment>().Any().Should().BeFalse();
    }

    [Fact]
    public async Task PaymentFailed_ReleasesStockAndFailsOrder()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));
        await IsInState(saga, m => m.AwaitingPayment);

        await Publish(new PaymentFailed(_orderId, "Card declined"));

        (await IsInState(saga, m => m.Failed)).Should().BeTrue();
        (await _harness.Sent.Any<ReleaseStock>(x => x.Context.Message.OrderId == _orderId)).Should().BeTrue();
        (await _harness.Published.Any<OrderFailed>()).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentTimeout_ReleasesStockRefundsAndFailsOrder()
    {
        var saga = await StartAsync(paymentTimeoutSeconds: 1);
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));

        (await IsInState(saga, m => m.Failed)).Should().BeTrue();
        (await _harness.Sent.Any<ReleaseStock>()).Should().BeTrue();
        (await _harness.Sent.Any<RefundPayment>()).Should().BeTrue();
        (await _harness.Published.Any<OrderFailed>(x => x.Context.Message.Reason == "Payment timed out.")).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentCompletedBeforeTimeout_DoesNotFailLater()
    {
        var saga = await StartAsync(paymentTimeoutSeconds: 1);
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));
        await IsInState(saga, m => m.AwaitingPayment);
        await Publish(new PaymentCompleted(_orderId, "txn_1"));
        await IsInState(saga, m => m.Confirmed);

        await Task.Delay(TimeSpan.FromSeconds(2));

        (await IsInState(saga, m => m.Confirmed)).Should().BeTrue();
        _harness.Published.Select<OrderFailed>().Any().Should().BeFalse();
    }

    [Fact]
    public async Task CancelWhileAwaitingPayment_UndoesStockAndPayment()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));
        await IsInState(saga, m => m.AwaitingPayment);

        await Publish(new OrderCancelled(_orderId, CustomerId, "changed my mind"));

        (await IsInState(saga, m => m.Cancelled)).Should().BeTrue();
        (await _harness.Sent.Any<ReleaseStock>()).Should().BeTrue();
        (await _harness.Sent.Any<RefundPayment>()).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentCompletesAfterCancel_IsRefunded()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await Publish(new StockReserved(_orderId));
        await IsInState(saga, m => m.AwaitingPayment);
        await Publish(new OrderCancelled(_orderId, CustomerId, "changed my mind"));
        await IsInState(saga, m => m.Cancelled);

        await Publish(new PaymentCompleted(_orderId, "txn_late"));
        await Task.Delay(500);

        _harness.Sent.Select<RefundPayment>().Count().Should().Be(2);
    }

    [Fact]
    public async Task StockReservedAfterCancel_IsReleased()
    {
        var saga = await StartAsync();
        await Publish(_submitted);
        await IsInState(saga, m => m.AwaitingStock);
        await Publish(new OrderCancelled(_orderId, CustomerId, "changed my mind"));
        await IsInState(saga, m => m.Cancelled);

        await Publish(new StockReserved(_orderId));
        await Task.Delay(500);

        _harness.Sent.Select<ReleaseStock>().Count().Should().Be(2);
        _harness.Sent.Select<ChargePayment>().Any().Should().BeFalse();
    }
}
