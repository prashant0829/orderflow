using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Application.Common;
using OrderFlow.Payments.Infrastructure.Gateway;
using OrderFlow.Payments.Infrastructure.Gateway.Stripe;
using Stripe;
using ChargeOutcome = OrderFlow.Payments.Application.Abstractions.ChargeOutcome;

namespace OrderFlow.Payments.UnitTests;

internal sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));
        return respond(request, body);
    }
}

public class StripeGatewayTests
{
    private const string IntentJson = """{"id":"pi_123","object":"payment_intent","status":"requires_payment_method","client_secret":"pi_123_secret_abc","currency":"usd","amount":1050}""";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static StripePaymentGateway Gateway(StubHandler handler) =>
        new(
            new StripeClient(new StripeClientOptions
            {
                ApiKey = "sk_test_stub",
                ApiBase = "https://stripe.test",
                HttpClient = new SystemNetHttpClient(new HttpClient(handler), maxNetworkRetries: 0)
            }),
            Options.Create(new StripeOptions { PublishableKey = "pk_test_stub" }),
            Options.Create(new PaymentsOptions { Provider = PaymentsOptions.StripeProvider, Currency = "usd" }));

    [Fact]
    public async Task Charge_CreatesAPaymentIntent_InCents_WithAnIdempotencyKeyPerOrder()
    {
        var handler = new StubHandler((_, _) => Json(IntentJson));
        var orderId = Guid.NewGuid();

        var result = await Gateway(handler).ChargeAsync(orderId, 10.5m, CancellationToken.None);

        result.Outcome.Should().Be(ChargeOutcome.ActionRequired);
        result.TransactionId.Should().Be("pi_123");
        var (request, body) = handler.Calls.Single();
        request.RequestUri!.AbsolutePath.Should().Be("/v1/payment_intents");
        body.Should().Contain("amount=1050").And.Contain("currency=usd").And.Contain(Uri.EscapeDataString(orderId.ToString()));
        request.Headers.GetValues("Idempotency-Key").Single().Should().Be($"orderflow-payment-{orderId}");
    }

    [Theory]
    [InlineData(10.5, 1050)]
    [InlineData(0.01, 1)]
    [InlineData(10.005, 1001)]
    [InlineData(4851.00, 485100)]
    public void MoneyIsConvertedToCents(double amount, long cents)
    {
        StripePaymentGateway.ToMinorUnits((decimal)amount).Should().Be(cents);
    }

    [Fact]
    public async Task StripeServerError_IsATemporaryProblem_ThatTheRetryPolicyCanHandle()
    {
        var handler = new StubHandler((_, _) => Json("""{"error":{"type":"api_error","message":"boom"}}""", HttpStatusCode.InternalServerError));

        var act = () => Gateway(handler).ChargeAsync(Guid.NewGuid(), 5m, CancellationToken.None);

        await act.Should().ThrowAsync<PaymentGatewayUnavailableException>();
    }

    [Fact]
    public async Task BadRequest_IsNotRetriedAsIfItWereAnOutage()
    {
        var handler = new StubHandler((_, _) => Json("""{"error":{"type":"invalid_request_error","message":"Invalid currency"}}""", HttpStatusCode.BadRequest));

        var act = () => Gateway(handler).ChargeAsync(Guid.NewGuid(), 5m, CancellationToken.None);

        await act.Should().ThrowAsync<StripeException>();
    }

    [Fact]
    public async Task Checkout_ReturnsTheClientSecretAndPublishableKey()
    {
        var handler = new StubHandler((_, _) => Json(IntentJson));

        var checkout = await Gateway(handler).GetCheckoutAsync("pi_123", CancellationToken.None);

        checkout!.ClientSecret.Should().Be("pi_123_secret_abc");
        checkout.PublishableKey.Should().Be("pk_test_stub");
        checkout.Currency.Should().Be("usd");
    }

    [Theory]
    [InlineData("succeeded", ProviderPaymentState.Succeeded)]
    [InlineData("canceled", ProviderPaymentState.Canceled)]
    [InlineData("processing", ProviderPaymentState.Processing)]
    [InlineData("requires_payment_method", ProviderPaymentState.RequiresPayment)]
    [InlineData("requires_action", ProviderPaymentState.RequiresPayment)]
    public async Task StatusIsMapped(string stripeStatus, ProviderPaymentState expected)
    {
        var handler = new StubHandler((_, _) => Json($$"""{"id":"pi_123","object":"payment_intent","status":"{{stripeStatus}}"}"""));

        (await Gateway(handler).GetStatusAsync("pi_123", CancellationToken.None)).Should().Be(expected);
    }

    [Fact]
    public async Task Cancel_WhenTheCustomerAlreadyPaid_IsRefused_NotForced()
    {
        var handler = new StubHandler((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/cancel", StringComparison.Ordinal)
            ? Json("""{"error":{"type":"invalid_request_error","code":"payment_intent_unexpected_state","message":"already succeeded"}}""", HttpStatusCode.BadRequest)
            : Json("""{"id":"pi_123","object":"payment_intent","status":"succeeded"}"""));

        (await Gateway(handler).CancelAsync("pi_123", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_Works_WhenNobodyPaid()
    {
        var handler = new StubHandler((_, _) => Json("""{"id":"pi_123","object":"payment_intent","status":"canceled"}"""));

        (await Gateway(handler).CancelAsync("pi_123", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Refund_UsesAnIdempotencyKey_AndToleratesAnEarlierRefund()
    {
        var handler = new StubHandler((_, _) => Json("""{"error":{"type":"invalid_request_error","code":"charge_already_refunded","message":"already refunded"}}""", HttpStatusCode.BadRequest));

        var act = () => Gateway(handler).RefundAsync("pi_123", CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.Calls.Single().Request.Headers.GetValues("Idempotency-Key").Single().Should().Be("orderflow-refund-pi_123");
    }
}

public class StripeWebhookVerifierTests
{
    private const string Secret = "whsec_test_secret";

    private static StripeWebhookVerifier Verifier(string secret = Secret) => new(Options.Create(new StripeOptions { WebhookSecret = secret }));

    private static string Event(string type, string intentId = "pi_123") =>
        $$$$"""{"id":"evt_1","object":"event","api_version":"2020-08-27","created":1700000000,"livemode":false,"type":"{{{{type}}}}","data":{"object":{"id":"{{{{intentId}}}}","object":"payment_intent","status":"succeeded","amount":1050,"currency":"usd"}}}""";

    /// <summary>Stripe's signature scheme: HMAC-SHA256 over "timestamp.body" with the endpoint secret.</summary>
    private static string Sign(string body, string secret = Secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    [Theory]
    [InlineData("payment_intent.succeeded", ProviderPaymentOutcome.Succeeded)]
    [InlineData("payment_intent.payment_failed", ProviderPaymentOutcome.Failed)]
    [InlineData("payment_intent.canceled", ProviderPaymentOutcome.Canceled)]
    public void ValidSignedEvents_BecomeUpdates(string type, ProviderPaymentOutcome expected)
    {
        var body = Event(type);

        var update = Verifier().Verify(body, Sign(body));

        update.Should().Be(new ProviderPaymentUpdate("pi_123", expected));
    }

    [Fact]
    public void EventTypesWeDoNotCareAbout_AreAcceptedAndIgnored()
    {
        var body = Event("payment_intent.created");

        Verifier().Verify(body, Sign(body)).Should().BeNull();
    }

    [Fact]
    public void WrongSecret_IsRejected()
    {
        var body = Event("payment_intent.succeeded");

        var act = () => Verifier().Verify(body, Sign(body, secret: "whsec_someone_else"));

        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void TamperedBody_IsRejected()
    {
        var body = Event("payment_intent.succeeded");
        var header = Sign(body);

        var act = () => Verifier().Verify(body.Replace("1050", "1"), header);

        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void ReplayedOldEvent_IsRejected()
    {
        var body = Event("payment_intent.succeeded");

        var act = () => Verifier().Verify(body, Sign(body, at: DateTimeOffset.UtcNow.AddHours(-2)));

        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void MissingHeader_IsRejected()
    {
        var act = () => Verifier().Verify(Event("payment_intent.succeeded"), null);

        act.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public void WithoutASecret_TheEndpointIsOff()
    {
        Verifier(secret: "").IsEnabled.Should().BeFalse();
        Verifier().IsEnabled.Should().BeTrue();
    }
}
