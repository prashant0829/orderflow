using MassTransit;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Inventory;
using OrderFlow.Contracts.Orders;
using OrderFlow.Contracts.Payments;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Infrastructure.Sagas;

/// <summary>
/// Orchestrates one order: reserve stock, charge payment, confirm. When a step fails the earlier steps are undone
/// (release stock, refund). Events that arrive late (after the order already failed or was cancelled) are compensated too.
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine(IOptions<OrderSagaOptions> options)
    {
        InstanceState(saga => saga.CurrentState);

        ConfigureEvents();
        Schedule(() => PaymentTimeout, saga => saga.PaymentTimeoutTokenId, schedule =>
        {
            schedule.Delay = TimeSpan.FromSeconds(options.Value.PaymentTimeoutSeconds);
            schedule.Received = received => received.CorrelateById(context => context.Message.OrderId);
        });

        Initially(
            When(OrderSubmittedEvent)
                .Then(context =>
                {
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.CreatedAtUtc = DateTime.UtcNow;
                    Touch(context.Saga);
                })
                .Send(Queue(QueueNames.ReserveStock), context => new ReserveStock(context.Saga.CorrelationId, context.Message.Lines))
                .Publish(context => Status(context.Saga, OrderProgressStates.AwaitingStock, "Order received. Reserving your items."))
                .TransitionTo(AwaitingStock));

        During(AwaitingStock,
            When(StockReservedEvent)
                .Send(Queue(QueueNames.ChargePayment), context => new ChargePayment(context.Saga.CorrelationId, context.Saga.CustomerId, context.Saga.TotalAmount))
                .Schedule(PaymentTimeout, context => new PaymentTimeoutExpired(context.Saga.CorrelationId))
                .Then(context => Touch(context.Saga))
                .Publish(context => Status(context.Saga, OrderProgressStates.AwaitingPayment, "Items reserved. Processing payment."))
                .TransitionTo(AwaitingPayment),
            When(StockReservationFailedEvent)
                .Then(context => Fail(context.Saga, context.Message.Reason))
                .Publish(context => new OrderFailed(context.Saga.CorrelationId, context.Saga.CustomerId, context.Saga.FailureReason!))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Failed, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Failed, context.Saga.FailureReason!))
                .TransitionTo(Failed),
            When(OrderCancelledEvent)
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId))
                .Then(context => Cancel(context.Saga, context.Message.Reason))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Cancelled, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Cancelled, "Order cancelled."))
                .TransitionTo(Cancelled),
            Ignore(PaymentCompletedEvent),
            Ignore(PaymentFailedEvent),
            Ignore(PaymentTimeout.Received));

        During(AwaitingPayment,
            When(PaymentCompletedEvent)
                .Unschedule(PaymentTimeout)
                .Then(context =>
                {
                    context.Saga.PaymentTransactionId = context.Message.TransactionId;
                    Touch(context.Saga);
                })
                .Publish(context => new OrderConfirmed(context.Saga.CorrelationId, context.Saga.CustomerId))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Confirmed, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Confirmed, "Payment received. Your order is confirmed."))
                .TransitionTo(Confirmed),
            When(PaymentFailedEvent)
                .Unschedule(PaymentTimeout)
                .Then(context => Fail(context.Saga, context.Message.Reason))
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId))
                .Publish(context => new OrderFailed(context.Saga.CorrelationId, context.Saga.CustomerId, context.Saga.FailureReason!))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Failed, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Failed, context.Saga.FailureReason!))
                .TransitionTo(Failed),
            When(PaymentTimeout.Received)
                .Then(context => Fail(context.Saga, "Payment timed out."))
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId))
                .Send(Queue(QueueNames.RefundPayment), context => new RefundPayment(context.Saga.CorrelationId, "Payment timed out"))
                .Publish(context => new OrderFailed(context.Saga.CorrelationId, context.Saga.CustomerId, context.Saga.FailureReason!))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Failed, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Failed, context.Saga.FailureReason!))
                .TransitionTo(Failed),
            When(OrderCancelledEvent)
                .Unschedule(PaymentTimeout)
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId))
                .Send(Queue(QueueNames.RefundPayment), context => new RefundPayment(context.Saga.CorrelationId, context.Message.Reason))
                .Then(context => Cancel(context.Saga, context.Message.Reason))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Cancelled, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Cancelled, "Order cancelled. Items released and payment refunded."))
                .TransitionTo(Cancelled),
            Ignore(StockReservedEvent),
            Ignore(StockReservationFailedEvent));

        During(Confirmed,
            // The customer cancelled while the confirmation was still travelling: undo everything.
            When(OrderCancelledEvent)
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId))
                .Send(Queue(QueueNames.RefundPayment), context => new RefundPayment(context.Saga.CorrelationId, context.Message.Reason))
                .Then(context => Cancel(context.Saga, context.Message.Reason))
                .Then(context => OrdersTelemetry.RecordCompleted(OrdersTelemetry.Cancelled, context.Saga.CreatedAtUtc))
                .Publish(context => Status(context.Saga, OrderProgressStates.Cancelled, "Order cancelled. Items released and payment refunded."))
                .TransitionTo(Cancelled),
            Ignore(StockReservedEvent),
            Ignore(StockReservationFailedEvent),
            Ignore(PaymentCompletedEvent),
            Ignore(PaymentFailedEvent),
            Ignore(PaymentTimeout.Received));

        During(Failed, Cancelled,
            // Late results from steps that were still in flight when the order ended: give the resources back.
            When(StockReservedEvent)
                .Send(Queue(QueueNames.ReleaseStock), context => new ReleaseStock(context.Saga.CorrelationId)),
            When(PaymentCompletedEvent)
                .Send(Queue(QueueNames.RefundPayment), context => new RefundPayment(context.Saga.CorrelationId, "Order already ended")),
            Ignore(StockReservationFailedEvent),
            Ignore(PaymentFailedEvent),
            Ignore(OrderCancelledEvent),
            Ignore(PaymentTimeout.Received));

        DuringAny(Ignore(OrderSubmittedEvent));
    }

    public State AwaitingStock { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmittedEvent { get; private set; } = null!;
    public Event<StockReserved> StockReservedEvent { get; private set; } = null!;
    public Event<StockReservationFailed> StockReservationFailedEvent { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompletedEvent { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailedEvent { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelledEvent { get; private set; } = null!;

    public Schedule<OrderState, PaymentTimeoutExpired> PaymentTimeout { get; private set; } = null!;

    private static OrderStatusChanged Status(OrderState saga, string state, string message) =>
        new(saga.CorrelationId, saga.CustomerId, state, message, DateTime.UtcNow);

    private static Uri Queue(string queueName) => new($"queue:{queueName}");

    private static void Touch(OrderState saga) => saga.UpdatedAtUtc = DateTime.UtcNow;

    private static void Fail(OrderState saga, string reason)
    {
        saga.FailureReason = reason;
        Touch(saga);
    }

    private static void Cancel(OrderState saga, string reason)
    {
        saga.FailureReason = reason;
        Touch(saga);
    }

    private void ConfigureEvents()
    {
        Event(() => OrderSubmittedEvent, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReservedEvent, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReservationFailedEvent, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentCompletedEvent, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentFailedEvent, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderCancelledEvent, e => e.CorrelateById(context => context.Message.OrderId));
    }
}
