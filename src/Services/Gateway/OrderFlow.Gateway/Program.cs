using OrderFlow.Gateway;
using OrderFlow.Gateway.RateLimiting;
using OrderFlow.Observability;
using OrderFlow.Security;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrderFlowSerilog("gateway");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "gateway", OrderFlow.Gateway.GatewayTelemetry.MeterName);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddOrderFlowAuthentication(builder.Configuration);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("X-Correlation-Id", "Retry-After", "X-RateLimit-Limit", "X-RateLimit-Remaining")));

builder.Services.AddHttpClient("dependency-health");
builder.Services.AddHealthChecks().AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RateLimitMiddleware>();
app.UseAuthorization();

app.MapOrderFlowHealth();
app.MapDependencyHealth();
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
