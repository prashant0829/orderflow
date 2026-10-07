using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Cqrs;

namespace OrderFlow.Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services) =>
        services.AddOrderFlowCqrs(typeof(DependencyInjection).Assembly);
}
