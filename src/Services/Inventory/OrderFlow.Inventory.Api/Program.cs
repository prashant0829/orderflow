using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Application;
using OrderFlow.Inventory.Infrastructure;
using OrderFlow.Inventory.Infrastructure.Messaging;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Messaging;
using OrderFlow.Observability;
using OrderFlow.Security;
using OrderFlow.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrderFlowSerilog("inventory-api");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "inventory-api", OrderFlow.Inventory.Application.Common.InventoryTelemetry.MeterName);

builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddOrderFlowMessaging(builder.Configuration, "inventory", registration => registration.AddInventoryMessaging());

builder.Services.AddOrderFlowAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOrderFlowApiDefaults();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString(InventoryDbContext.ConnectionStringName)!, name: "sqlserver", tags: [HealthEndpoints.ReadyTag])
    .AddRedis(builder.Configuration.GetConnectionString(OrderFlow.Inventory.Infrastructure.DependencyInjection.RedisConnectionStringName)!, name: "redis", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await dbContext.Database.MigrateAsync();
    await InventorySeeder.SeedAsync(dbContext);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOrderFlowHealth();

await app.RunAsync();

public partial class Program;
