using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Caching;
using OrderFlow.Orders.Infrastructure.Catalog;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Sagas;

namespace OrderFlow.Orders.Infrastructure;

public static class DependencyInjection
{
    public const string RedisConnectionStringName = "Redis";

    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString(PersistenceConstants.ConnectionStringName),
            sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderProgressReader, OrderProgressReader>();
        services.Configure<OrderSagaOptions>(configuration.GetSection(OrderSagaOptions.SectionName));

        services.AddOrderFlowRedisCache(configuration.GetConnectionString(RedisConnectionStringName), "orderflow:");

        var catalogOptions = configuration.GetSection(InventoryCatalogOptions.SectionName).Get<InventoryCatalogOptions>() ?? new InventoryCatalogOptions();
        services.AddHttpClient<IProductCatalog, InventoryProductCatalog>(client => client.BaseAddress = new Uri(catalogOptions.BaseUrl))
            .AddStandardResilienceHandler();

        return services;
    }
}
