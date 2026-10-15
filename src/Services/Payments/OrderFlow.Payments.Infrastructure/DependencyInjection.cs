using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Infrastructure.Gateway;
using OrderFlow.Payments.Infrastructure.Gateway.Stripe;
using OrderFlow.Payments.Infrastructure.Messaging;
using OrderFlow.Payments.Infrastructure.Persistence;
using Stripe;

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
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();

        services.Configure<PaymentsOptions>(configuration.GetSection(PaymentsOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<FakePaymentGatewayOptions>(configuration.GetSection(FakePaymentGatewayOptions.SectionName));
        services.Configure<PaymentResilienceOptions>(configuration.GetSection(PaymentResilienceOptions.SectionName));

        // Singleton: the circuit breaker must remember failures across requests.
        services.AddSingleton(provider => PaymentGatewayResilience.Create(
            provider.GetRequiredService<IOptions<PaymentResilienceOptions>>().Value,
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("PaymentGatewayResilience")));

        var payments = configuration.GetSection(PaymentsOptions.SectionName).Get<PaymentsOptions>() ?? new PaymentsOptions();
        if (string.Equals(payments.Provider, PaymentsOptions.StripeProvider, StringComparison.OrdinalIgnoreCase))
        {
            AddStripe(services, configuration);
        }
        else
        {
            services.AddSingleton<IPaymentGateway>(provider => Decorate(provider, new FakePaymentGateway(provider.GetRequiredService<IOptions<FakePaymentGatewayOptions>>())));
            services.AddSingleton<IWebhookVerifier, DisabledWebhookVerifier>();
        }

        return services;
    }

    private static void AddStripe(IServiceCollection services, IConfiguration configuration)
    {
        var stripe = configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>() ?? new StripeOptions();
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            throw new InvalidOperationException(
                "Payments:Provider is Stripe but Stripe:SecretKey is empty. Set Stripe__SecretKey (sk_test_...) and Stripe__PublishableKey (pk_test_...), or use Payments__Provider=Fake.");
        }

        services.AddSingleton<IStripeClient>(_ => new StripeClient(new StripeClientOptions
        {
            ApiKey = stripe.SecretKey,
            ApiBase = string.IsNullOrWhiteSpace(stripe.ApiBase) ? null : stripe.ApiBase,

            // Retries are done once, by our own pipeline; the idempotency keys make them safe.
            HttpClient = new SystemNetHttpClient(maxNetworkRetries: 0)
        }));
        services.AddSingleton<IPaymentGateway>(provider => Decorate(provider, new StripePaymentGateway(
            provider.GetRequiredService<IStripeClient>(),
            provider.GetRequiredService<IOptions<StripeOptions>>(),
            provider.GetRequiredService<IOptions<PaymentsOptions>>())));
        services.AddSingleton<IWebhookVerifier, StripeWebhookVerifier>();
    }

    private static ResilientPaymentGateway Decorate(IServiceProvider provider, IPaymentGateway inner) =>
        new(inner, provider.GetRequiredService<Polly.ResiliencePipeline>());
}
