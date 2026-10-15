using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderFlow.Contracts.Payments;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Payments.Commands;
using OrderFlow.Payments.Application.Payments.Queries;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.UnitTests;

/// <summary>Payments where the customer has to pay (Stripe): create, wait, then complete, cancel or refund.</summary>
public class ProviderFlowTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly IPaymentRepository _repository = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIntegrationEventPublisher _publisher = Substitute.For<IIntegrationEventPublisher>();

    private Payment WaitingPayment()
    {
        var payment = Payment.AwaitingCustomer(OrderId, CustomerId, 25m, "pi_1");
        _repository.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(payment);
        _repository.GetByProviderPaymentIdAsync("pi_1", Arg.Any<CancellationToken>()).Returns(payment);
        return payment;
    }

    private ApplyProviderPaymentUpdateCommandHandler Apply() =>
        new(_repository, _publisher, _unitOfWork, NullLogger<ApplyProviderPaymentUpdateCommandHandler>.Instance);

    [Fact]
    public async Task Charge_WhenTheProviderNeedsTheCustomer_SavesAWaitingPaymentAndAnnouncesNothing()
    {
        _gateway.ChargeAsync(OrderId, 25m, Arg.Any<CancellationToken>()).Returns(GatewayChargeResult.RequireAction("pi_1"));

        var result = await new ChargePaymentCommandHandler(_repository, _gateway, _unitOfWork)
            .Handle(new ChargePaymentCommand(OrderId, CustomerId, 25m), CancellationToken.None);

        result.Kind.Should().Be(ChargeResultKind.Pending);
        await _repository.Received(1).AddAsync(Arg.Is<Payment>(payment => payment.Status == PaymentStatus.AwaitingCustomer && payment.ProviderPaymentId == "pi_1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Charge_SecondDeliveryWhileWaiting_StaysPendingWithoutCallingTheProvider()
    {
        WaitingPayment();

        var result = await new ChargePaymentCommandHandler(_repository, _gateway, _unitOfWork)
            .Handle(new ChargePaymentCommand(OrderId, CustomerId, 25m), CancellationToken.None);

        result.Kind.Should().Be(ChargeResultKind.Pending);
        await _gateway.DidNotReceive().ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderSaysSucceeded_CompletesThePayment_AndAnnouncesItOnce()
    {
        var payment = WaitingPayment();
        var handler = Apply();

        await handler.Handle(new ApplyProviderPaymentUpdateCommand("pi_1", ProviderPaymentOutcome.Succeeded), CancellationToken.None);
        await handler.Handle(new ApplyProviderPaymentUpdateCommand("pi_1", ProviderPaymentOutcome.Succeeded), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Completed);
        await _publisher.Received(1).PublishAsync(Arg.Is<PaymentCompleted>(message => message.OrderId == OrderId && message.TransactionId == "pi_1"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderEventForAnUnknownPayment_IsIgnored()
    {
        var result = await Apply().Handle(new ApplyProviderPaymentUpdateCommand("pi_other", ProviderPaymentOutcome.Succeeded), CancellationToken.None);

        result.Should().BeNull();
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync<PaymentCompleted>(default!, default);
    }

    [Fact]
    public async Task AFailedCardAttempt_IsNotFinal_TheCustomerCanTryAgain()
    {
        var payment = WaitingPayment();

        await Apply().Handle(new ApplyProviderPaymentUpdateCommand("pi_1", ProviderPaymentOutcome.Failed), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.AwaitingCustomer);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync<PaymentFailed>(default!, default);
    }

    [Fact]
    public async Task ProviderSaysCancelled_MarksThePaymentCancelled()
    {
        var payment = WaitingPayment();

        await Apply().Handle(new ApplyProviderPaymentUpdateCommand("pi_1", ProviderPaymentOutcome.Canceled), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task Refresh_WhenTheProviderReportsSuccess_CompletesThePayment()
    {
        var payment = WaitingPayment();
        _gateway.GetStatusAsync("pi_1", Arg.Any<CancellationToken>()).Returns(ProviderPaymentState.Succeeded);
        var sender = Substitute.For<MediatR.ISender>();
        sender.Send(Arg.Any<ApplyProviderPaymentUpdateCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Apply().Handle(call.Arg<ApplyProviderPaymentUpdateCommand>(), CancellationToken.None));

        await new RefreshPaymentCommandHandler(_repository, _gateway, sender).Handle(new RefreshPaymentCommand(OrderId), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task Refresh_WhileTheCustomerHasNotPaid_ChangesNothing()
    {
        var payment = WaitingPayment();
        _gateway.GetStatusAsync("pi_1", Arg.Any<CancellationToken>()).Returns(ProviderPaymentState.RequiresPayment);
        var sender = Substitute.For<MediatR.ISender>();
        sender.Send(Arg.Any<ApplyProviderPaymentUpdateCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Apply().Handle(call.Arg<ApplyProviderPaymentUpdateCommand>(), CancellationToken.None));

        await new RefreshPaymentCommandHandler(_repository, _gateway, sender).Handle(new RefreshPaymentCommand(OrderId), CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.AwaitingCustomer);
    }

    [Fact]
    public async Task Refund_OfAnUnpaidPayment_CancelsItAtTheProvider()
    {
        var payment = WaitingPayment();
        _gateway.CancelAsync("pi_1", Arg.Any<CancellationToken>()).Returns(true);

        var done = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        done.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Cancelled);
        await _gateway.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_WhenTheCustomerPaidAtThatVeryMoment_LeavesItForTheCompletionEventToRefund()
    {
        var payment = WaitingPayment();
        _gateway.CancelAsync("pi_1", Arg.Any<CancellationToken>()).Returns(false);

        var done = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        done.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.AwaitingCustomer);
    }

    [Fact]
    public async Task Refund_OfACancelledPayment_IsDone()
    {
        var payment = WaitingPayment();
        payment.Cancel();

        var done = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        done.Should().BeTrue();
        await _gateway.DidNotReceiveWithAnyArgs().CancelAsync(default!, default);
    }

    [Fact]
    public async Task Checkout_GivesTheClientSecretOnlyWhileTheCustomerStillHasToPay()
    {
        var payment = WaitingPayment();
        _gateway.GetCheckoutAsync("pi_1", Arg.Any<CancellationToken>()).Returns(new PaymentCheckout("Stripe", "pk_test_x", "pi_1_secret_y", "usd"));
        var handler = new GetCheckoutQueryHandler(_repository, _gateway);

        var waiting = await handler.Handle(new GetCheckoutQuery(OrderId), CancellationToken.None);
        payment.Complete("pi_1");
        var paid = await handler.Handle(new GetCheckoutQuery(OrderId), CancellationToken.None);

        waiting.ClientSecret.Should().Be("pi_1_secret_y");
        waiting.PublishableKey.Should().Be("pk_test_x");
        paid.ClientSecret.Should().BeNull();
        paid.Status.Should().Be("Completed");
    }
}
