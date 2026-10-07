using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Infrastructure.Gateway;

namespace OrderFlow.Payments.UnitTests;

public class ResilienceTests
{
    private readonly IPaymentGateway _inner = Substitute.For<IPaymentGateway>();

    private ResilientPaymentGateway Create(int retryCount, int minimumThroughput = 100) =>
        new(_inner, PaymentGatewayResilience.Create(
            new PaymentResilienceOptions
            {
                RetryCount = retryCount,
                RetryBaseDelayMs = 1,
                MinimumThroughput = minimumThroughput,
                BreakDurationSeconds = 30
            },
            NullLogger.Instance));

    [Fact]
    public async Task TemporaryFailure_IsRetriedThenSucceeds()
    {
        _inner.ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new PaymentGatewayUnavailableException(), _ => throw new PaymentGatewayUnavailableException(), _ => GatewayChargeResult.Approve("txn_ok"));

        var result = await Create(retryCount: 3).ChargeAsync(Guid.NewGuid(), 10m, CancellationToken.None);

        result.Approved.Should().BeTrue();
        await _inner.Received(3).ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistentFailure_StopsAfterConfiguredRetries()
    {
        _inner.ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns<GatewayChargeResult>(_ => throw new PaymentGatewayUnavailableException());

        var act = () => Create(retryCount: 2).ChargeAsync(Guid.NewGuid(), 10m, CancellationToken.None);

        await act.Should().ThrowAsync<PaymentGatewayUnavailableException>();
        await _inner.Received(3).ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Decline_IsNotRetried()
    {
        _inner.ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(GatewayChargeResult.Decline("no"));

        var result = await Create(retryCount: 3).ChargeAsync(Guid.NewGuid(), 10m, CancellationToken.None);

        result.Approved.Should().BeFalse();
        await _inner.Received(1).ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterRepeatedFailures_CircuitOpensAndCallsFailFast()
    {
        _inner.ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns<GatewayChargeResult>(_ => throw new PaymentGatewayUnavailableException());
        var gateway = Create(retryCount: 0, minimumThroughput: 2);

        for (var i = 0; i < 2; i++)
        {
            await gateway.Invoking(g => g.ChargeAsync(Guid.NewGuid(), 10m, CancellationToken.None)).Should().ThrowAsync<PaymentGatewayUnavailableException>();
        }

        _inner.ClearReceivedCalls();
        await gateway.Invoking(g => g.ChargeAsync(Guid.NewGuid(), 10m, CancellationToken.None)).Should().ThrowAsync<PaymentGatewayUnavailableException>();

        await _inner.DidNotReceive().ChargeAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }
}
