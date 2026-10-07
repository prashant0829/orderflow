using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Infrastructure.Gateway;
using OrderFlow.Payments.Infrastructure.Persistence;

namespace OrderFlow.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentsDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString(PaymentsDbContext.ConnectionStringName),
            sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, PaymentsUnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        services.Configure<FakePaymentGatewayOptions>(configuration.GetSection(FakePaymentGatewayOptions.SectionName));
        services.Configure<PaymentResilienceOptions>(configuration.GetSection(PaymentResilienceOptions.SectionName));

        // Singleton: the circuit breaker must remember failures across requests.
        services.AddSingleton(provider => PaymentGatewayResilience.Create(
            provider.GetRequiredService<IOptions<PaymentResilienceOptions>>().Value,
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("PaymentGatewayResilience")));

        services.AddSingleton<FakePaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider => new ResilientPaymentGateway(
            provider.GetRequiredService<FakePaymentGateway>(),
            provider.GetRequiredService<Polly.ResiliencePipeline>()));

        return services;
    }
}
