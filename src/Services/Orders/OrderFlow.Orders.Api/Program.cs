using Microsoft.EntityFrameworkCore;
using MassTransit;
using OrderFlow.Messaging;
using OrderFlow.Messaging.DeadLetters;
using OrderFlow.Observability;
using OrderFlow.Security;
using OrderFlow.Web;
using OrderFlow.Orders.Application;
using OrderFlow.Orders.Infrastructure;
using OrderFlow.Orders.Infrastructure.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrderFlowSerilog("orders-api");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "orders-api", OrderFlow.Orders.Application.Common.OrdersTelemetry.MeterName);

builder.Services.AddOrdersApplication();
builder.Services.AddOrdersInfrastructure(builder.Configuration);
builder.Services.AddOrderFlowMessaging(
    builder.Configuration,
    "orders",
    registration => registration.AddOrdersMessaging(),
    (bus, _) => bus.UseDelayedMessageScheduler());
builder.Services.AddOrderFlowDeadLetterTools(builder.Configuration);

builder.Services.AddOrderFlowAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOrderFlowApiDefaults();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString(PersistenceConstants.ConnectionStringName)!, name: "sqlserver", tags: [HealthEndpoints.ReadyTag])
    .AddRedis(builder.Configuration.GetConnectionString(OrderFlow.Orders.Infrastructure.DependencyInjection.RedisConnectionStringName)!, name: "redis", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOrderFlowHealth();

await app.RunAsync();

public partial class Program;
