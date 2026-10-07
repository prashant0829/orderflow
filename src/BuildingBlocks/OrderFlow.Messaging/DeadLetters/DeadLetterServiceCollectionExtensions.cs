using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OrderFlow.Messaging.DeadLetters;

public static class DeadLetterServiceCollectionExtensions
{
    public static IServiceCollection AddOrderFlowDeadLetterTools(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(section);
        var options = section.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddHttpClient(DeadLetterConstants.ManagementHttpClientName, client =>
        {
            client.BaseAddress = new Uri(options.ManagementUrl.TrimEnd('/') + "/");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        });

        services.TryAddSingleton<IDeadLetterService, RabbitMqDeadLetterService>();
        return services;
    }
}
