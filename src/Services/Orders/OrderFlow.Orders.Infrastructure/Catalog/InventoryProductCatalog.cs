using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure.Catalog;

/// <summary>Reads authoritative prices from the Inventory service over HTTP (retry, circuit breaker and timeout come from the HttpClient pipeline).</summary>
public class InventoryProductCatalog(HttpClient httpClient, ILogger<InventoryProductCatalog> logger) : IProductCatalog
{
    private sealed record LookupRequest(IReadOnlyCollection<Guid> ProductIds);

    private sealed record StockItemResponse(Guid ProductId, string Name, decimal UnitPrice);

    public async Task<IReadOnlyDictionary<Guid, ProductInfo>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                new Uri("api/v1/stock/lookup", UriKind.Relative), new LookupRequest(productIds), cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<StockItemResponse>>(cancellationToken) ?? [];
            return items.ToDictionary(item => item.ProductId, item => new ProductInfo(item.ProductId, item.Name, item.UnitPrice));
        }
        catch (Exception exception) when (exception is HttpRequestException or BrokenCircuitException or TimeoutRejectedException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Inventory catalog lookup failed");
            throw new DependencyUnavailableException("The product catalog is temporarily unavailable. Please try again.", exception);
        }
    }
}
