using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Messaging;
using OrderFlow.Notifications;
using OrderFlow.Notifications.Consumers;
using OrderFlow.Notifications.Persistence;
using OrderFlow.Notifications.Realtime;
using OrderFlow.Notifications.Sending;
using OrderFlow.Observability;
using OrderFlow.Security;
using OrderFlow.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOrderFlowSerilog(builder.Configuration, "notifications");
builder.Services.AddOrderFlowTelemetry(builder.Configuration, "notifications", OrderFlow.Notifications.NotificationsTelemetry.MeterName);

builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString(NotificationsDbContext.ConnectionStringName),
    sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<IOrderStatusNotifier, SignalROrderStatusNotifier>();

builder.Services.AddOrderFlowMessaging(builder.Configuration, "notifications", registration =>
{
    registration.AddConsumer<OrderSubmittedConsumer>();
    registration.AddConsumer<OrderConfirmedConsumer>();
    registration.AddConsumer<OrderFailedConsumer>();
    registration.AddConsumer<OrderCancelledConsumer>();
    registration.AddConsumer<OrderStatusChangedConsumer>();

    // Inbox: remembers processed MessageIds so a redelivered message is not handled twice.
    registration.AddEntityFrameworkOutbox<NotificationsDbContext>(outbox => outbox.UseSqlServerWithOwnSchema<NotificationsDbContext>());
    registration.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<NotificationsDbContext>(context));
});

builder.Services.AddOrderFlowAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOrderFlowApiDefaults();

// The Redis backplane lets several Notifications instances push to the same browsers.
builder.Services.AddSignalR().AddStackExchangeRedis(
    builder.Configuration.GetConnectionString("Redis")!,
    options => options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("orderflow-signalr"));

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString(NotificationsDbContext.ConnectionStringName)!, name: "sqlserver", tags: [HealthEndpoints.ReadyTag])
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis", tags: [HealthEndpoints.ReadyTag]);

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>(RealtimeConstants.HubPath);
app.MapOrderFlowHealth();

await app.RunAsync();

public partial class Program;
