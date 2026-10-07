using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;
using OrderFlow.Observability;
using OrderFlow.Payments.Application;
using OrderFlow.Payments.Infrastructure;
using OrderFlow.Payments.Infrastructure.Messaging;
using OrderFlow.Payments.Infrastructure.Persistence;
using OrderFlow.Security;
using OrderFlow.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOrderFlowSerilog("payments-api");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "payments-api", OrderFlow.Payments.Application.Common.PaymentsTelemetry.MeterName);

builder.Services.AddPaymentsApplication();
builder.Services.AddPaymentsInfrastructure(builder.Configuration);
builder.Services.AddOrderFlowMessaging(builder.Configuration, "payments", registration => registration.AddPaymentsMessaging());

builder.Services.AddOrderFlowAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOrderFlowApiDefaults();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString(PaymentsDbContext.ConnectionStringName)!, name: "sqlserver", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapOrderFlowHealth();

await app.RunAsync();

public partial class Program;
