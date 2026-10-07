using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Cqrs;

namespace OrderFlow.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services) =>
        services.AddOrderFlowCqrs(typeof(DependencyInjection).Assembly);
}
