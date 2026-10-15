using FluentAssertions;
using OrderFlow.Payments.Domain.Payments;
using OrderFlow.SharedKernel;

namespace OrderFlow.Payments.UnitTests;

public class PaymentStateTests
{
    private static Payment Waiting() => Payment.AwaitingCustomer(Guid.NewGuid(), Guid.NewGuid(), 10m, "pi_1");

    [Fact]
    public void AwaitingCustomer_StartsWaitingWithTheProviderId()
    {
        var payment = Waiting();

        payment.Status.Should().Be(PaymentStatus.AwaitingCustomer);
        payment.ProviderPaymentId.Should().Be("pi_1");
        payment.TransactionId.Should().BeNull();
    }

    [Fact]
    public void Complete_FromWaiting_SetsTheTransaction_AndCanBeRepeated()
    {
        var payment = Waiting();

        payment.Complete("pi_1");
        payment.Complete("pi_1");

        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionId.Should().Be("pi_1");
    }

    [Fact]
    public void Complete_AfterCancel_IsRejected()
    {
        var payment = Waiting();
        payment.Cancel();

        var act = () => payment.Complete("pi_1");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_FromWaiting_Works_AndCanBeRepeated()
    {
        var payment = Waiting();

        payment.Cancel();
        payment.Cancel();

        payment.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AfterPayment_IsRejected()
    {
        var payment = Waiting();
        payment.Complete("pi_1");

        var act = payment.Cancel;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Refund_OnlyAfterPayment()
    {
        var waiting = Waiting();

        var act = waiting.Refund;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AwaitingCustomer_WithoutProviderId_IsRejected()
    {
        var act = () => Payment.AwaitingCustomer(Guid.NewGuid(), Guid.NewGuid(), 10m, " ");

        act.Should().Throw<DomainException>();
    }
}
