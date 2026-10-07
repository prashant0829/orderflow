using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Cqrs;

namespace OrderFlow.Payments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services) =>
        services.AddOrderFlowCqrs(typeof(DependencyInjection).Assembly);
}
