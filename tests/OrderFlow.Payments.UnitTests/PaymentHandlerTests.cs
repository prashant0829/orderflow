using FluentAssertions;
using NSubstitute;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Application.Payments.Commands;
using OrderFlow.Payments.Domain.Payments;

namespace OrderFlow.Payments.UnitTests;

public class PaymentHandlerTests
{
    private readonly IPaymentRepository _repository = Substitute.For<IPaymentRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly ChargePaymentCommand Charge = new(OrderId, CustomerId, 100m);

    private ChargePaymentCommandHandler ChargeHandler => new(_repository, _gateway, _unitOfWork);

    [Fact]
    public async Task Charge_Approved_SavesCompletedPayment()
    {
        _gateway.ChargeAsync(OrderId, 100m, Arg.Any<CancellationToken>()).Returns(GatewayChargeResult.Approve("txn_1"));

        var result = await ChargeHandler.Handle(Charge, CancellationToken.None);

        result.Should().Be(ChargePaymentResult.Succeeded("txn_1"));
        await _repository.Received(1).AddAsync(Arg.Is<Payment>(payment => payment.Status == PaymentStatus.Completed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Charge_Declined_SavesFailedPaymentWithReason()
    {
        _gateway.ChargeAsync(OrderId, 100m, Arg.Any<CancellationToken>()).Returns(GatewayChargeResult.Decline("No funds"));

        var result = await ChargeHandler.Handle(Charge, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("No funds");
    }

    [Fact]
    public async Task Charge_GatewayUnavailable_FailsPaymentInsteadOfThrowing()
    {
        _gateway.ChargeAsync(OrderId, 100m, Arg.Any<CancellationToken>()).Returns<GatewayChargeResult>(_ => throw new PaymentGatewayUnavailableException());

        var result = await ChargeHandler.Handle(Charge, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("unavailable");
    }

    [Fact]
    public async Task Charge_SecondDelivery_ReturnsFirstOutcomeAndNeverCallsGateway()
    {
        _repository.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(Payment.Completed(OrderId, CustomerId, 100m, "txn_1"));

        var result = await ChargeHandler.Handle(Charge, CancellationToken.None);

        result.TransactionId.Should().Be("txn_1");
        await _gateway.DidNotReceive().ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_CompletedPayment_RefundsAndReturnsTrue()
    {
        var payment = Payment.Completed(OrderId, CustomerId, 100m, "txn_1");
        _repository.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(payment);

        var refunded = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        refunded.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        await _gateway.Received(1).RefundAsync("txn_1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_AlreadyRefunded_DoesNotCallGatewayAgain()
    {
        var payment = Payment.Completed(OrderId, CustomerId, 100m, "txn_1");
        payment.Refund();
        _repository.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(payment);

        var refunded = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        refunded.Should().BeTrue();
        await _gateway.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_FailedOrMissingPayment_ReturnsFalse()
    {
        _repository.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(Payment.Failed(OrderId, CustomerId, 100m, "declined"));

        var refunded = await new RefundPaymentCommandHandler(_repository, _gateway, _unitOfWork).Handle(new RefundPaymentCommand(OrderId), CancellationToken.None);

        refunded.Should().BeFalse();
    }
}
