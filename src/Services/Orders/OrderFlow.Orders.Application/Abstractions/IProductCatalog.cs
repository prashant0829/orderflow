namespace OrderFlow.Orders.Application.Abstractions;

public record ProductInfo(Guid ProductId, string Name, decimal UnitPrice);

public interface IProductCatalog
{
    /// <summary>Returns the products that exist; unknown ids are simply missing from the result.</summary>
    /// <exception cref="OrderFlow.SharedKernel.DependencyUnavailableException">The catalog service cannot be reached.</exception>
    Task<IReadOnlyDictionary<Guid, ProductInfo>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}
