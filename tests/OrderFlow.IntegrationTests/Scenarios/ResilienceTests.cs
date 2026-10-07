using FluentAssertions;
using OrderFlow.IntegrationTests.Infrastructure;
using Xunit;

namespace OrderFlow.IntegrationTests.Scenarios;

[Collection(DefaultSystemCollection.Name)]
public class ResilienceTests(DefaultSystem system)
{
    private readonly Api _api = new(system);

    [Fact]
    public async Task OrderIsAccepted_WhileTheMessageBrokerIsDown_AndCompletesOnceItIsBack()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 15m, stock: 10);

        await Api.DockerAsync("pause", system.RabbitContainerId);
        OrderDto order;
        try
        {
            // The order and its "submitted" message are saved in one SQL transaction (the outbox), so the broker is not needed here.
            var response = await _api.PlaceOrderAsync(customer, product.ProductId, 1);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
            order = await Api.ReadAsync<OrderDto>(response);

            await Task.Delay(TimeSpan.FromSeconds(3));
            (await _api.ProgressAsync(customer, order.Id)).State.Should().Be("Submitted", "nothing can move while the broker is paused");
        }
        finally
        {
            await Api.DockerAsync("unpause", system.RabbitContainerId);
        }

        await _api.WaitForStateAsync(customer, order.Id, "Confirmed", seconds: 90);
        (await _api.StockOfAsync(product.ProductId)).Should().Be(9);
        (await _api.PaymentOfAsync(admin, order.Id))!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task RefreshTokens_Rotate_AndAStolenOneEndsEverySession()
    {
        var customer = await _api.RegisterCustomerAsync();

        var rotated = await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = customer.RefreshToken });
        rotated.IsSuccessStatusCode.Should().BeTrue();
        using var rotatedBody = System.Text.Json.JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        var newRefresh = rotatedBody.RootElement.GetProperty("refreshToken").GetString()!;
        newRefresh.Should().NotBe(customer.RefreshToken);

        var replay = await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = customer.RefreshToken });
        replay.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var afterTheft = await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/refresh", body: new { refreshToken = newRefresh });
        afterTheft.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized, "re-using an old token revokes the whole family");
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var customer = await _api.RegisterCustomerAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/login", body: new { email = customer.Email, password = "wrong" }))
                .StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        var locked = await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/login", body: new { email = customer.Email, password = "Passw0rd!x" });

        locked.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync()).Should().Contain("Too many failed attempts");
    }

    [Fact]
    public async Task DuplicateRegistration_IsRejected_ByTheDatabaseConstraintToo()
    {
        var customer = await _api.RegisterCustomerAsync();

        var again = await _api.SendAsync(system.Identity, HttpMethod.Post, "/api/v1/auth/register", body: new { email = customer.Email, password = "Passw0rd!x" });

        again.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdminOrderList_CanFilterByStatus()
    {
        var admin = await _api.AdminAsync();
        var customer = await _api.RegisterCustomerAsync();
        var product = await _api.CreateProductAsync(admin, price: 10m, stock: 1);
        var failing = await _api.PlaceOrderOkAsync(customer, product.ProductId, 5);
        await _api.WaitForStateAsync(customer, failing.Id, "Failed");
        await Wait.UntilAsync(async () => (await _api.GetOrderAsync(customer, failing.Id)).Status == "Failed", TimeSpan.FromSeconds(15), "order row to be Failed");

        var response = await _api.SendAsync(system.Orders, HttpMethod.Get, "/api/v1/admin/orders?status=Failed&pageSize=100", admin.AccessToken);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Contain(failing.Id.ToString());
        (await _api.SendAsync(system.Orders, HttpMethod.Get, "/api/v1/admin/orders?status=Banana", admin.AccessToken)).StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await _api.SendAsync(system.Orders, HttpMethod.Get, "/api/v1/admin/orders", customer.AccessToken)).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
