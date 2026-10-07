using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Infrastructure.Persistence;

namespace OrderFlow.Inventory.Infrastructure;

public static class DependencyInjection
{
    public const string RedisConnectionStringName = "Redis";

    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString(InventoryDbContext.ConnectionStringName),
            sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();

        services.AddOrderFlowRedisCache(configuration.GetConnectionString(RedisConnectionStringName), "orderflow:");

        return services;
    }
}
