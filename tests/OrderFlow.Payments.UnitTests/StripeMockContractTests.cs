using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Infrastructure.Gateway;
using OrderFlow.Payments.Infrastructure.Gateway.Stripe;
using Stripe;
using Xunit;
using ChargeOutcome = OrderFlow.Payments.Application.Abstractions.ChargeOutcome;

namespace OrderFlow.Payments.UnitTests;

/// <summary>Runs only when STRIPE_MOCK_URL is set (see docs/RUNBOOK.md).</summary>
public sealed class StripeMockFactAttribute : FactAttribute
{
    public const string UrlVariable = "STRIPE_MOCK_URL";

    public StripeMockFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UrlVariable)))
        {
            Skip = $"Set {UrlVariable}=http://localhost:12111 and run `docker run -p 12111:12111 stripe/stripe-mock` to check the requests against Stripe's real API specification.";
        }
    }
}

/// <summary>
/// stripe-mock is Stripe's official mock server. It rejects any request that does not match Stripe's OpenAPI specification
/// (unknown fields, wrong types, missing parameters), so these tests prove our requests are valid without needing an account.
/// </summary>
public class StripeMockContractTests
{
    private static StripePaymentGateway Gateway() =>
        new(
            new StripeClient(new StripeClientOptions
            {
                ApiKey = "sk_test_mock",
                ApiBase = Environment.GetEnvironmentVariable(StripeMockFactAttribute.UrlVariable),
                HttpClient = new SystemNetHttpClient(maxNetworkRetries: 0)
            }),
            Options.Create(new StripeOptions { PublishableKey = "pk_test_mock" }),
            Options.Create(new PaymentsOptions { Provider = PaymentsOptions.StripeProvider, Currency = "usd" }));

    [StripeMockFact]
    public async Task CreatingAPaymentIntent_IsAValidStripeRequest()
    {
        var result = await Gateway().ChargeAsync(Guid.NewGuid(), 129.49m, CancellationToken.None);

        result.Outcome.Should().Be(ChargeOutcome.ActionRequired);
        result.TransactionId.Should().StartWith("pi_");
    }

    [StripeMockFact]
    public async Task ReadingCheckoutAndStatus_AreValidStripeRequests()
    {
        var gateway = Gateway();

        var checkout = await gateway.GetCheckoutAsync("pi_123", CancellationToken.None);
        var status = await gateway.GetStatusAsync("pi_123", CancellationToken.None);

        checkout!.ClientSecret.Should().NotBeNullOrEmpty();
        checkout.PublishableKey.Should().Be("pk_test_mock");
        Enum.IsDefined(status).Should().BeTrue();
    }

    [StripeMockFact]
    public async Task CancellingAndRefunding_AreValidStripeRequests()
    {
        var gateway = Gateway();

        var cancelled = () => gateway.CancelAsync("pi_123", CancellationToken.None);
        var refunded = () => gateway.RefundAsync("pi_123", CancellationToken.None);

        await cancelled.Should().NotThrowAsync();
        await refunded.Should().NotThrowAsync();
    }
}
