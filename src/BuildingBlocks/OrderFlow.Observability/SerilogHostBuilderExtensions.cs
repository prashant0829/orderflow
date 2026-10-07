using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace OrderFlow.Observability;

public static class SerilogHostBuilderExtensions
{
    public static IHostBuilder UseOrderFlowSerilog(this IHostBuilder host, string serviceName) =>
        host.UseSerilog((context, _, logger) => ConfigureLogger(logger, context.Configuration, serviceName));

    public static IServiceCollection AddOrderFlowSerilog(this IServiceCollection services, IConfiguration configuration, string serviceName) =>
        services.AddSerilog(logger => ConfigureLogger(logger, configuration, serviceName));

    private static void ConfigureLogger(LoggerConfiguration logger, IConfiguration configuration, string serviceName) => logger
        .ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .Enrich.With<ActivityEnricher>()
        .Enrich.WithProperty("Service", serviceName)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.Seq(configuration["Seq:Url"] ?? "http://localhost:5341");
}
