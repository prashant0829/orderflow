using System.Net;
using FluentAssertions;
using OrderFlow.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderFlow.IntegrationTests.Scenarios;

/// <summary>Payments take 3.5 s but the saga only waits 2 s, so results arrive after the order has already ended.</summary>
[Collection(SlowPaymentCollection.Name)]
public class SlowPaymentTests(SlowPaymentSystem system)
{
    private readonly Api _api = new(system);

    [Fact]
    public async Task PaymentTakingTooLong_FailsTheOrder_ReleasesStock_AndRefundsTheLatePayment()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 40m, stock: 5);

        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 1);
        var progress = await _api.WaitForStateAsync(customer, order.Id, "Failed", seconds: 30);

        progress.FailureReason.Should().Be("Payment timed out.");
        await Wait.UntilAsync(async () => await _api.StockOfAsync(product.ProductId) == 5, TimeSpan.FromSeconds(20), "the stock to be released");
        await Wait.UntilAsync(async () => (await _api.PaymentOfAsync(admin, order.Id))?.Status == "Refunded", TimeSpan.FromSeconds(30),
            "the late payment to be refunded automatically");
    }

    [Fact]
    public async Task CancellingWhilePaymentIsInFlight_UndoesStockAndPayment()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 30m, stock: 5);

        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 2);
        await _api.WaitForStateAsync(customer, order.Id, "AwaitingPayment", seconds: 30);
        var cancel = await _api.CancelAsync(customer, order.Id);

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        await _api.WaitForStateAsync(customer, order.Id, "Cancelled", seconds: 30);
        await Wait.UntilAsync(async () => await _api.StockOfAsync(product.ProductId) == 5, TimeSpan.FromSeconds(20), "the stock to be released");
        await Wait.UntilAsync(async () => (await _api.PaymentOfAsync(admin, order.Id))?.Status == "Refunded", TimeSpan.FromSeconds(30),
            "the payment to be refunded");
        (await _api.GetOrderAsync(customer, order.Id)).Status.Should().Be("Cancelled");
    }
}
