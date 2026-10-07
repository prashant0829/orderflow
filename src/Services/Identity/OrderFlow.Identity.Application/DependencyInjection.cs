using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Cqrs;
using OrderFlow.Identity.Application.Auth;
using OrderFlow.Identity.Application.Common;

namespace OrderFlow.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOrderFlowCqrs(typeof(DependencyInjection).Assembly);
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
