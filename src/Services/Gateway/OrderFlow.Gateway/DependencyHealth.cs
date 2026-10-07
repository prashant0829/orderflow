using System.Diagnostics;
using OrderFlow.Security;

namespace OrderFlow.Gateway;

public record ServiceHealth(string Service, string Status, int DurationMs, string? Error);

public static class DependencyHealth
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// GET /health/dependencies (admin only): calls /health/ready on every service behind the gateway, in parallel.
    /// Gives one place to see which part of the system is down.
    /// </summary>
    public static IEndpointRouteBuilder MapDependencyHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/dependencies", async (IConfiguration configuration, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
        {
            var services = configuration.GetSection("ReverseProxy:Clusters").GetChildren()
                .Select(cluster => (Name: cluster.Key, Address: cluster.GetSection("Destinations").GetChildren().FirstOrDefault()?["Address"]))
                .Where(service => !string.IsNullOrEmpty(service.Address))
                .ToList();

            var results = await Task.WhenAll(services.Select(service => CheckAsync(service.Name, service.Address!, httpClientFactory, cancellationToken)));
            var overall = results.All(result => result.Status == "Healthy") ? "Healthy" : "Degraded";

            return Results.Json(new { status = overall, services = results.OrderBy(result => result.Service) });
        }).RequireAuthorization(PolicyNames.AdminOnly);

        return endpoints;
    }

    private static async Task<ServiceHealth> CheckAsync(string name, string address, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CheckTimeout);

            var client = httpClientFactory.CreateClient("dependency-health");
            using var response = await client.GetAsync(new Uri(new Uri(address), "health/ready"), timeout.Token);
            return new ServiceHealth(name, response.IsSuccessStatusCode ? "Healthy" : "Unhealthy", (int)stopwatch.ElapsedMilliseconds,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new ServiceHealth(name, "Unreachable", (int)stopwatch.ElapsedMilliseconds, exception.Message);
        }
    }
}
