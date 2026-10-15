using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using OrderFlow.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderFlow.IntegrationTests.Scenarios;

/// <summary>The Stripe-style flow: the order waits for the customer to pay, and the result comes by webhook or when the page asks.</summary>
[Collection(CardPaymentCollection.Name)]
public class CardPaymentTests(CardPaymentSystem system)
{
    private readonly Api _api = new(system);

    private CardGatewayDouble Stripe => system.CardGateway!;

    private static string WebhookBody(string type, string paymentId) =>
        JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            api_version = "2020-08-27",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            livemode = false,
            type,
            data = new { @object = new { id = paymentId, @object = "payment_intent", status = "succeeded", amount = 1000, currency = "usd" } }
        });

    /// <summary>Stripe's signature scheme: HMAC-SHA256 over "timestamp.body" with the endpoint secret.</summary>
    private static string Sign(string body, string secret = SystemFixture.CardWebhookSecret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string? signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/stripe") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (signature is not null) request.Headers.Add("Stripe-Signature", signature);
        return await system.Payments.SendAsync(request);
    }

    private async Task<JsonElement> CheckoutAsync(Session who, Guid orderId)
    {
        var response = await _api.SendAsync(system.Payments, HttpMethod.Get, $"/api/v1/payments/order/{orderId}/checkout", who.AccessToken);
        return (await Api.ReadAsync<JsonElement>(response));
    }

    private async Task<(Session Customer, OrderDto Order, string PaymentId)> OrderWaitingForPaymentAsync(decimal price = 20m)
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price, stock: 5);
        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 1);
        await _api.WaitForStateAsync(customer, order.Id, "AwaitingPayment", seconds: 30);
        await Wait.UntilAsync(async () => (await _api.SendAsync(system.Payments, HttpMethod.Get, $"/api/v1/payments/order/{order.Id}/checkout", customer.AccessToken)).IsSuccessStatusCode,
            TimeSpan.FromSeconds(20), "the payment to be created");
        return (customer, order, CardGatewayDouble.PaymentIdFor(order.Id));
    }

    [Fact]
    public async Task TheOrderWaitsForTheCustomer_ThenConfirmsWhenStripeSaysThePaymentSucceeded()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();

        var checkout = await CheckoutAsync(customer, order.Id);
        checkout.GetProperty("status").GetString().Should().Be("AwaitingCustomer");
        checkout.GetProperty("clientSecret").GetString().Should().Be($"{paymentId}_secret_double");
        checkout.GetProperty("publishableKey").GetString().Should().Be("pk_test_double");
        (await _api.ProgressAsync(customer, order.Id)).State.Should().Be("AwaitingPayment", "nobody has paid yet");

        Stripe.CustomerPays(paymentId);
        var body = WebhookBody("payment_intent.succeeded", paymentId);
        (await PostWebhookAsync(body, Sign(body))).StatusCode.Should().Be(HttpStatusCode.OK);

        var progress = await _api.WaitForStateAsync(customer, order.Id, "Confirmed");
        progress.PaymentTransactionId.Should().Be(paymentId);
        (await CheckoutAsync(customer, order.Id)).GetProperty("clientSecret").ValueKind.Should().Be(JsonValueKind.Null, "the secret is only handed out while payment is pending");
    }

    [Fact]
    public async Task WithoutAWebhook_AskingForARefreshAfterPayingConfirmsTheOrder()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();
        Stripe.CustomerPays(paymentId);

        var refresh = await _api.SendAsync(system.Payments, HttpMethod.Post, $"/api/v1/payments/order/{order.Id}/refresh", customer.AccessToken);

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        await _api.WaitForStateAsync(customer, order.Id, "Confirmed");
    }

    [Fact]
    public async Task TheWebhookAndTheRefreshTogether_ConfirmTheOrderOnlyOnce()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();
        Stripe.CustomerPays(paymentId);
        var body = WebhookBody("payment_intent.succeeded", paymentId);

        await Task.WhenAll(
            PostWebhookAsync(body, Sign(body)),
            PostWebhookAsync(body, Sign(body)),
            _api.SendAsync(system.Payments, HttpMethod.Post, $"/api/v1/payments/order/{order.Id}/refresh", customer.AccessToken));

        await _api.WaitForStateAsync(customer, order.Id, "Confirmed");
        await Wait.UntilAsync(async () => (await _api.TimelineAsync(customer, order.Id)).Count(entry => entry.State == "Confirmed") == 1, TimeSpan.FromSeconds(15), "a single confirmation");
        Stripe.Refunded.Should().NotContain(paymentId, "a payment confirmed once is never refunded");
    }

    [Fact]
    public async Task CancellingBeforePaying_CancelsThePaymentAtStripe()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();

        (await _api.CancelAsync(customer, order.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        await _api.WaitForStateAsync(customer, order.Id, "Cancelled");
        await Wait.UntilAsync(() => Task.FromResult(Stripe.Cancelled.Contains(paymentId)), TimeSpan.FromSeconds(20), "the Stripe payment to be cancelled");
        (await _api.PaymentOfAsync(await _api.AdminAsync(), order.Id))!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task NobodyPays_TheOrderTimesOut_AndThePaymentIsCancelled()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();

        var progress = await _api.WaitForStateAsync(customer, order.Id, "Failed", seconds: 40);

        progress.FailureReason.Should().Be("Payment timed out.");
        await Wait.UntilAsync(() => Task.FromResult(Stripe.Cancelled.Contains(paymentId)), TimeSpan.FromSeconds(20), "the unpaid Stripe payment to be cancelled");
    }

    [Fact]
    public async Task CustomerPaysAfterTheOrderAlreadyTimedOut_TheMoneyIsRefundedAutomatically()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();
        Stripe.RefuseCancel(paymentId); // the customer is paying at this very moment: Stripe will not let us cancel
        await _api.WaitForStateAsync(customer, order.Id, "Failed", seconds: 40);

        Stripe.CustomerPays(paymentId);
        var body = WebhookBody("payment_intent.succeeded", paymentId);
        (await PostWebhookAsync(body, Sign(body))).StatusCode.Should().Be(HttpStatusCode.OK);

        await Wait.UntilAsync(async () => (await _api.PaymentOfAsync(await _api.AdminAsync(), order.Id))?.Status == "Refunded", TimeSpan.FromSeconds(30), "the late payment to be refunded");
        Stripe.Refunded.Should().Contain(paymentId);
    }

    [Fact]
    public async Task AFailedCardAttempt_DoesNotEndTheOrder()
    {
        var (customer, order, paymentId) = await OrderWaitingForPaymentAsync();

        var body = WebhookBody("payment_intent.payment_failed", paymentId);
        (await PostWebhookAsync(body, Sign(body))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await CheckoutAsync(customer, order.Id)).GetProperty("status").GetString().Should().Be("AwaitingCustomer", "the customer can try another card");
        (await _api.ProgressAsync(customer, order.Id)).State.Should().Be("AwaitingPayment");
    }

    [Fact]
    public async Task Webhooks_WithABadOrMissingSignature_AreRejected_AndUnknownPaymentsIgnored()
    {
        var (_, _, paymentId) = await OrderWaitingForPaymentAsync();
        var body = WebhookBody("payment_intent.succeeded", paymentId);

        (await PostWebhookAsync(body, Sign(body, secret: "whsec_attacker"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostWebhookAsync(body, null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostWebhookAsync(body.Replace("1000", "1"), Sign(body))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var unknown = WebhookBody("payment_intent.succeeded", "pi_someone_elses");
        (await PostWebhookAsync(unknown, Sign(unknown))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnlyTheOwnerCanSeeTheCheckoutOfAnOrder()
    {
        var (_, order, _) = await OrderWaitingForPaymentAsync();
        var stranger = await _api.RegisterCustomerAsync();

        (await _api.SendAsync(system.Payments, HttpMethod.Get, $"/api/v1/payments/order/{order.Id}/checkout", stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(system.Payments, HttpMethod.Post, $"/api/v1/payments/order/{order.Id}/refresh", stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(system.Payments, HttpMethod.Get, $"/api/v1/payments/order/{order.Id}/checkout")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>With the fake provider there are no webhooks, so the endpoint stays off.</summary>
[Collection(DefaultSystemCollection.Name)]
public class WebhookDisabledTests(DefaultSystem system)
{
    [Fact]
    public async Task WithTheFakeProvider_TheWebhookEndpointIsNotAvailable()
    {
        var response = await system.Payments.PostAsJsonAsync("/api/v1/payments/webhooks/stripe", new { type = "payment_intent.succeeded" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
