using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace OrderFlow.Security;

public static class AuthenticationExtensions
{
    private const string HubPathPrefix = "/hubs";
    private const string AccessTokenQueryKey = "access_token";
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    public static JwtOptions GetJwtOptions(this IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyLength} bytes. Set it in configuration or the Jwt__SigningKey environment variable.");
        }

        return options;
    }

    /// <summary>Validates bearer tokens signed by the Identity service. Every service calls this, so a request that skips the gateway is still checked.</summary>
    public static IServiceCollection AddOrderFlowAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetJwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = AllowedClockSkew,
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                // Browsers cannot set headers on a WebSocket, so SignalR sends the token in the query string.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query[AccessTokenQueryKey];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix, StringComparison.Ordinal))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.Authenticated, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(Roles.Admin));

        return services;
    }
}

public static class PolicyNames
{
    public const string AdminOnly = "AdminOnly";
    public const string Authenticated = "Authenticated";
}
