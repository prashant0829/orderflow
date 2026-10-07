using System.Net;
using FluentAssertions;
using OrderFlow.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderFlow.IntegrationTests.Scenarios;

[Collection(DefaultSystemCollection.Name)]
public class OrderFlowTests(DefaultSystem system)
{
    private readonly Api _api = new(system);

    [Fact]
    public async Task EveryServiceReportsReady()
    {
        foreach (var client in new[] { system.Identity, system.Inventory, system.Payments, system.Orders, system.Notifications })
        {
            var response = await client.GetAsync("/health/ready");
            (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task PlacingAnOrder_ReservesStock_TakesPayment_Confirms_AndNotifiesTheCustomer()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 25m, stock: 10);

        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 2);

        order.TotalAmount.Should().Be(50m, "the price comes from Inventory, not from the client");
        await _api.WaitForStateAsync(customer, order.Id, "Confirmed");
        await Wait.UntilAsync(async () => (await _api.GetOrderAsync(customer, order.Id)).Status == "Confirmed", TimeSpan.FromSeconds(15), "the order row to become Confirmed");

        (await _api.StockOfAsync(product.ProductId)).Should().Be(8);
        var payment = await _api.PaymentOfAsync(admin, order.Id);
        payment!.Status.Should().Be("Completed");
        payment.Amount.Should().Be(50m);

        await Wait.UntilAsync(async () => (await _api.TimelineAsync(customer, order.Id)).Count >= 3, TimeSpan.FromSeconds(15), "the timeline to be recorded");
        (await _api.TimelineAsync(customer, order.Id)).Select(entry => entry.State).Should().Equal("AwaitingStock", "AwaitingPayment", "Confirmed");
        await Wait.UntilAsync(async () => (await _api.NotificationsAsync(customer)).Any(note => note.Type == "OrderConfirmed"), TimeSpan.FromSeconds(15), "the confirmation email");
    }

    [Fact]
    public async Task NotEnoughStock_FailsTheOrder_WithoutChargingAnyone()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 2);

        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 5);
        var progress = await _api.WaitForStateAsync(customer, order.Id, "Failed");

        progress.FailureReason.Should().Contain("Insufficient stock");
        (await _api.StockOfAsync(product.ProductId)).Should().Be(2);
        (await _api.PaymentOfAsync(admin, order.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeclinedCard_FailsTheOrder_AndGivesTheStockBack()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 13.13m, stock: 5); // 13.13 is the fake gateway's "always declined" amount

        var order = await _api.PlaceOrderOkAsync(customer, product.ProductId, quantity: 1);
        var progress = await _api.WaitForStateAsync(customer, order.Id, "Failed");

        progress.FailureReason.Should().Contain("declined");
        (await _api.PaymentOfAsync(admin, order.Id))!.Status.Should().Be("Failed");
        await Wait.UntilAsync(async () => await _api.StockOfAsync(product.ProductId) == 5, TimeSpan.FromSeconds(15), "the stock to be released");
    }

    [Fact]
    public async Task RetryingWithTheSameIdempotencyKey_ReturnsTheSameOrder_AndReservesOnce()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 10);
        var key = Guid.NewGuid().ToString();

        var first = await _api.PlaceOrderAsync(customer, product.ProductId, 3, key);
        var second = await _api.PlaceOrderAsync(customer, product.ProductId, 3, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Api.ReadAsync<OrderDto>(second)).Id.Should().Be((await Api.ReadAsync<OrderDto>(first)).Id);

        var orderId = (await Api.ReadAsync<OrderDto>(await _api.PlaceOrderAsync(customer, product.ProductId, 3, key))).Id;
        await _api.WaitForStateAsync(customer, orderId, "Confirmed");
        (await _api.StockOfAsync(product.ProductId)).Should().Be(7, "three items were reserved once, not three times");
    }

    [Fact]
    public async Task FiveParallelRequestsWithTheSameKey_CreateExactlyOneOrder()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 10);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _api.PlaceOrderAsync(customer, product.ProductId, 1, key)));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(4);
        var ids = await Task.WhenAll(responses.Select(Api.ReadAsync<OrderDto>));
        ids.Select(order => order.Id).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task TenCustomersRacingForThreeItems_NeverOversell()
    {
        var admin = await _api.AdminAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 3);
        var customers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _api.RegisterCustomerAsync()));

        var orders = await Task.WhenAll(customers.Select(async customer => (Customer: customer, Order: await _api.PlaceOrderOkAsync(customer, product.ProductId, 1))));

        var outcomes = new List<string>();
        foreach (var (customer, order) in orders)
        {
            string state = "";
            await Wait.UntilAsync(async () =>
            {
                state = (await _api.ProgressAsync(customer, order.Id)).State;
                return state is "Confirmed" or "Failed";
            }, TimeSpan.FromSeconds(60), $"order {order.Id} to finish");
            outcomes.Add(state);
        }

        outcomes.Count(state => state == "Confirmed").Should().Be(3);
        outcomes.Count(state => state == "Failed").Should().Be(7);
        (await _api.StockOfAsync(product.ProductId)).Should().Be(0);
    }

    [Fact]
    public async Task AnotherCustomer_CannotSeeOrCancelSomeoneElsesOrder()
    {
        var admin = await _api.AdminAsync();
        var owner = await _api.RegisterCustomerAsync();
        var stranger = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 5);
        var order = await _api.PlaceOrderOkAsync(owner, product.ProductId, 1);

        (await _api.SendAsync(system.Orders, HttpMethod.Get, $"/api/v1/orders/{order.Id}", stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(system.Orders, HttpMethod.Get, $"/api/v1/orders/{order.Id}/progress", stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.CancelAsync(stranger, order.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(system.Notifications, HttpMethod.Get, $"/api/v1/timeline/{order.Id}", stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK, "an unknown or foreign timeline is simply empty to the stranger");
        (await _api.GetOrderAsync(admin, order.Id)).Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectMissingTokens_AndCustomersOnAdminRoutes()
    {
        var customer = await _api.RegisterCustomerAsync();

        (await _api.SendAsync(system.Orders, HttpMethod.Get, "/api/v1/orders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _api.SendAsync(system.Payments, HttpMethod.Get, "/api/v1/payments", customer.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(system.Orders, HttpMethod.Get, "/api/v1/admin/dead-letters", customer.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(system.Inventory, HttpMethod.Post, "/api/v1/stock", customer.AccessToken, new { name = "x", unitPrice = 1, initialQuantity = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrderCannotBePlacedForAnUnknownProduct()
    {
        var customer = await _api.RegisterCustomerAsync();

        var response = await _api.PlaceOrderAsync(customer, Guid.NewGuid(), 1);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
