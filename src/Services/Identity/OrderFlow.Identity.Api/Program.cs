using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderFlow.Identity.Application;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Infrastructure;
using OrderFlow.Identity.Infrastructure.Persistence;
using OrderFlow.Observability;
using OrderFlow.Security;
using OrderFlow.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrderFlowSerilog("identity-api");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "identity-api", OrderFlow.Identity.Application.Common.IdentityTelemetry.MeterName);

builder.Services.AddIdentityApplication(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddOrderFlowAuthentication(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOrderFlowApiDefaults();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString(IdentityDbContext.ConnectionStringName)!, name: "sqlserver", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value,
        scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOrderFlowHealth();

await app.RunAsync();

public partial class Program;
