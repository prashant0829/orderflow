using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderFlow.IntegrationTests.Infrastructure;

public record Session(Guid UserId, string Email, string AccessToken, string RefreshToken);

public record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(Guid Id, Guid CustomerId, string Status, decimal TotalAmount, string? FailureReason, List<OrderLineDto> Lines);

public record ProgressDto(string State, string? FailureReason, string? PaymentTransactionId);

public record StockDto(Guid ProductId, string Name, decimal UnitPrice, int AvailableQuantity);

public record PaymentDto(Guid OrderId, decimal Amount, string Status, string? TransactionId, string? FailureReason);

public record TimelineDto(string State, string Message);

public record NotificationDto(string Type, string Subject);

/// <summary>A small typed client over the services in the fixture, so tests read like the user story they check.</summary>
public sealed class Api(SystemFixture system)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int _counter;

    public async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string? token = null, object? body = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        return await client.SendAsync(request);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    private async Task<Session> AuthenticateAsync(string path, object body)
    {
        var response = await SendAsync(system.Identity, HttpMethod.Post, path, body: body);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"{(int)response.StatusCode}: {document.RootElement}");

        var root = document.RootElement;
        return new Session(
            root.GetProperty("user").GetProperty("id").GetGuid(),
            root.GetProperty("user").GetProperty("email").GetString()!,
            root.GetProperty("accessToken").GetString()!,
            root.GetProperty("refreshToken").GetString()!);
    }

    public Task<Session> RegisterCustomerAsync() =>
        AuthenticateAsync("/api/v1/auth/register", new { email = $"customer{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.com", password = "Passw0rd!x", displayName = "Test" });

    public Task<Session> LoginAsync(string email, string password) => AuthenticateAsync("/api/v1/auth/login", new { email, password });

    public Task<Session> AdminAsync() => LoginAsync(SystemFixture.AdminEmail, SystemFixture.AdminPassword);

    public async Task<StockDto> CreateProductAsync(Session admin, decimal price, int stock, string? name = null) =>
        await ReadAsync<StockDto>(await SendAsync(system.Inventory, HttpMethod.Post, "/api/v1/stock", admin.AccessToken,
            new { name = name ?? $"Test product {Guid.NewGuid():N}"[..24], unitPrice = price, initialQuantity = stock }));

    public async Task<int> StockOfAsync(Guid productId) =>
        (await ReadAsync<StockDto>(await SendAsync(system.Inventory, HttpMethod.Get, $"/api/v1/stock/{productId}"))).AvailableQuantity;

    public Task<HttpResponseMessage> PlaceOrderAsync(Session customer, Guid productId, int quantity, string? idempotencyKey = null) =>
        SendAsync(system.Orders, HttpMethod.Post, "/api/v1/orders", customer.AccessToken,
            new { lines = new[] { new { productId, quantity } } }, idempotencyKey ?? Guid.NewGuid().ToString());

    public async Task<OrderDto> PlaceOrderOkAsync(Session customer, Guid productId, int quantity) =>
        await ReadAsync<OrderDto>(await PlaceOrderAsync(customer, productId, quantity));

    public async Task<OrderDto> GetOrderAsync(Session who, Guid orderId) =>
        await ReadAsync<OrderDto>(await SendAsync(system.Orders, HttpMethod.Get, $"/api/v1/orders/{orderId}", who.AccessToken));

    public async Task<ProgressDto> ProgressAsync(Session who, Guid orderId) =>
        await ReadAsync<ProgressDto>(await SendAsync(system.Orders, HttpMethod.Get, $"/api/v1/orders/{orderId}/progress", who.AccessToken));

    public Task<HttpResponseMessage> CancelAsync(Session who, Guid orderId) =>
        SendAsync(system.Orders, HttpMethod.Post, $"/api/v1/orders/{orderId}/cancel", who.AccessToken, new { reason = "changed my mind" });

    public async Task<PaymentDto?> PaymentOfAsync(Session admin, Guid orderId)
    {
        var response = await SendAsync(system.Payments, HttpMethod.Get, $"/api/v1/payments/order/{orderId}", admin.AccessToken);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<PaymentDto>(response);
    }

    public async Task<List<TimelineDto>> TimelineAsync(Session who, Guid orderId) =>
        await ReadAsync<List<TimelineDto>>(await SendAsync(system.Notifications, HttpMethod.Get, $"/api/v1/timeline/{orderId}", who.AccessToken));

    public async Task<List<NotificationDto>> NotificationsAsync(Session who) =>
        await ReadAsync<List<NotificationDto>>(await SendAsync(system.Notifications, HttpMethod.Get, "/api/v1/notifications", who.AccessToken));

    /// <summary>Polls until the saga reaches the state, failing with the last state seen if it never does.</summary>
    public async Task<ProgressDto> WaitForStateAsync(Session who, Guid orderId, string state, int seconds = 40)
    {
        ProgressDto? last = null;
        await Wait.UntilAsync(async () =>
        {
            last = await ProgressAsync(who, orderId);
            return last.State == state;
        }, TimeSpan.FromSeconds(seconds), $"order {orderId} to reach {state} (last seen: {last?.State ?? "nothing"}, reason: {last?.FailureReason})");
        return last!;
    }

    public static async Task<string> DockerAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
}

public static class Wait
{
    public static async Task UntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s waiting for {what}.");
    }
}
