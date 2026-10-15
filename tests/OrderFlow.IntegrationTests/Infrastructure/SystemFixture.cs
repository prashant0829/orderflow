using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrderFlow.Identity.Api.Controllers;
using OrderFlow.Inventory.Api.Controllers;
using OrderFlow.Notifications.Controllers;
using OrderFlow.Orders.Api.Controllers;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Infrastructure.Catalog;
using OrderFlow.Payments.Api.Controllers;
using OrderFlow.Payments.Application.Abstractions;
using OrderFlow.Payments.Infrastructure.Gateway.Stripe;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Real SQL Server, Redis and RabbitMQ in containers, with the real Identity, Inventory, Payments, Orders and Notifications
/// services running in this process on top of them. Nothing is mocked except the network address between Orders and Inventory.
/// Settings go in through environment variables because the services read configuration while the host is being built.
/// </summary>
public class SystemFixture(SystemOptions options) : IAsyncLifetime
{
    public const string AdminEmail = "admin@orderflow.local";
    public const string AdminPassword = "Admin#12345";
    private const string SqlPassword = "OrderFlow_Test#2026";
    private const string SigningKey = "integration-test-signing-key-0123456789abcdef";

    private readonly Dictionary<string, string?> _previousEnvironment = [];
    private MsSqlContainer? _sql;
    private RedisContainer? _redis;
    private RabbitMqContainer? _rabbit;

    private WebApplicationFactory<AuthController>? _identity;
    private WebApplicationFactory<StockController>? _inventory;
    private WebApplicationFactory<PaymentsController>? _payments;
    private WebApplicationFactory<OrdersController>? _orders;
    private WebApplicationFactory<NotificationsController>? _notifications;

    public HttpClient Identity { get; private set; } = null!;
    public HttpClient Inventory { get; private set; } = null!;
    public HttpClient Payments { get; private set; } = null!;
    public HttpClient Orders { get; private set; } = null!;
    public HttpClient Notifications { get; private set; } = null!;

    /// <summary>Only set when <see cref="SystemOptions.CustomerPaysByCard"/> is on.</summary>
    public CardGatewayDouble? CardGateway { get; private set; }

    public const string CardWebhookSecret = "whsec_integration_test_secret";

    public string RabbitContainerId => _rabbit?.Id ?? throw new InvalidOperationException("RabbitMQ is not running.");

    public async Task InitializeAsync()
    {
        await StartContainersAsync();
        ApplyEnvironment();

        // Dependencies first: Orders asks Inventory for prices, everything else only needs the databases and the broker.
        _identity = new WebApplicationFactory<AuthController>();
        Identity = _identity.CreateClient();
        _inventory = new WebApplicationFactory<StockController>();
        Inventory = _inventory.CreateClient();
        _payments = options.CustomerPaysByCard ? CreateCardPaymentsFactory() : new WebApplicationFactory<PaymentsController>();
        Payments = _payments.CreateClient();
        CardGateway = options.CustomerPaysByCard ? _payments.Services.GetRequiredService<CardGatewayDouble>() : null;
        _notifications = new WebApplicationFactory<NotificationsController>();
        Notifications = _notifications.CreateClient();

        _orders = new WebApplicationFactory<OrdersController>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            // Same typed client and code path as production, only the address is replaced by the in-process Inventory server.
            services.AddHttpClient<IProductCatalog, InventoryProductCatalog>(client => client.BaseAddress = new Uri("http://inventory.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => _inventory.Server.CreateHandler())));
        Orders = _orders.CreateClient();

        await WaitForDatabasesAsync();
    }

    /// <summary>
    /// In production the services migrate and seed before they start consuming messages. A test host starts the bus and runs the
    /// migration side by side, so wait here until every database is ready before any test sends traffic.
    /// </summary>
    private async Task WaitForDatabasesAsync()
    {
        var checks = new (string Database, string Query)[]
        {
            ("OrderFlow_Identity_Test", "SELECT COUNT(*) FROM [identity].[Users]"), // seeded demo admin and customer
            ("OrderFlow_Inventory_Test", "SELECT COUNT(*) FROM [inventory].[StockItems]"), // seeded products
            ("OrderFlow_Payments_Test", "SELECT COUNT(*) FROM [payments].[Payments]"),
            ("OrderFlow_Orders_Test", "SELECT COUNT(*) FROM [orders].[OrderSagas]"), // created by the last migration
            ("OrderFlow_Notifications_Test", "SELECT COUNT(*) FROM [notifications].[TimelineEntries]"),
        };

        foreach (var (database, query) in checks)
        {
            await Wait.UntilAsync(async () =>
            {
                try
                {
                    await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                        $"Server=localhost,{_sql!.GetMappedPublicPort(1433)};Database={database};User Id=sa;Password={SqlPassword};TrustServerCertificate=True;Encrypt=False");
                    await connection.OpenAsync();
                    await using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
                    var rows = (int)(await command.ExecuteScalarAsync())!;
                    return !database.Contains("Identity", StringComparison.Ordinal) && !database.Contains("Inventory", StringComparison.Ordinal) || rows > 0;
                }
                catch (Microsoft.Data.SqlClient.SqlException)
                {
                    return false; // database or table not there yet
                }
            }, TimeSpan.FromSeconds(180), $"{database} to be migrated");
        }
    }

    /// <summary>Real Payments service, but the provider is a stand-in for Stripe and webhooks are verified with a known secret.</summary>
    private static WebApplicationFactory<PaymentsController> CreateCardPaymentsFactory() =>
        new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<CardGatewayDouble>();
            services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<CardGatewayDouble>());
            services.RemoveAll<IWebhookVerifier>();
            services.AddSingleton<IWebhookVerifier>(_ => new StripeWebhookVerifier(Options.Create(new StripeOptions { WebhookSecret = CardWebhookSecret })));
        }));

    public async Task DisposeAsync()
    {
        foreach (var disposable in new IDisposable?[] { Orders, Notifications, Payments, Inventory, Identity, _orders, _notifications, _payments, _inventory, _identity })
        {
            disposable?.Dispose();
        }

        foreach (var container in new IAsyncDisposable?[] { _rabbit, _redis, _sql })
        {
            if (container is not null) await container.DisposeAsync();
        }

        foreach (var (name, value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private async Task StartContainersAsync()
    {
        var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword(SqlPassword);
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            // SQL Server only ships for x64; Docker Desktop runs it under emulation on Apple Silicon.
            sql = sql.WithCreateParameterModifier(parameters => parameters.Platform = "linux/amd64");
        }

        _sql = sql.Build();
        _redis = new RedisBuilder("redis:7-alpine").Build();

        // The custom image adds the delayed-message plugin that saga timeouts need.
        var rabbitImage = new ImageFromDockerfileBuilder()
            .WithName("orderflow-rabbitmq-test:3.13")
            .WithDockerfileDirectory(Path.Combine(CommonDirectoryPath.GetSolutionDirectory().DirectoryPath, "deploy", "rabbitmq"))
            .WithDockerfile("Dockerfile")
            .WithDeleteIfExists(false)
            .Build();
        await rabbitImage.CreateAsync();
        _rabbit = new RabbitMqBuilder(rabbitImage).WithUsername("orderflow").WithPassword("orderflow_test").Build();

        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync(), _rabbit.StartAsync());
    }

    private void ApplyEnvironment()
    {
        string Connection(string database) =>
            $"Server=localhost,{_sql!.GetMappedPublicPort(1433)};Database={database};User Id=sa;Password={SqlPassword};TrustServerCertificate=True;Encrypt=False";

        var settings = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development", // migrations and seeding only run in Development
            ["Telemetry__Enabled"] = "false",
            ["Seq__Url"] = "http://127.0.0.1:1",
            ["Jwt__SigningKey"] = SigningKey,
            ["Seed__AdminPassword"] = AdminPassword,
            ["Seed__CustomerPassword"] = "Customer#12345",
            ["RabbitMq__Host"] = _rabbit!.Hostname,
            ["RabbitMq__Port"] = _rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq__Username"] = "orderflow",
            ["RabbitMq__Password"] = "orderflow_test",
            ["ConnectionStrings__Redis"] = $"{_redis!.Hostname}:{_redis.GetMappedPublicPort(6379)}",
            ["ConnectionStrings__IdentityDb"] = Connection("OrderFlow_Identity_Test"),
            ["ConnectionStrings__InventoryDb"] = Connection("OrderFlow_Inventory_Test"),
            ["ConnectionStrings__PaymentsDb"] = Connection("OrderFlow_Payments_Test"),
            ["ConnectionStrings__OrdersDb"] = Connection("OrderFlow_Orders_Test"),
            ["ConnectionStrings__NotificationsDb"] = Connection("OrderFlow_Notifications_Test"),
            ["FakePaymentGateway__DeclineRate"] = "0",
            ["FakePaymentGateway__TransientFailureRate"] = "0",
            ["FakePaymentGateway__LatencyMs"] = options.PaymentLatencyMs.ToString(),
            ["OrderSaga__PaymentTimeoutSeconds"] = options.SagaPaymentTimeoutSeconds.ToString(),
        };

        foreach (var (name, value) in settings)
        {
            _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}

/// <summary>Default behaviour: fast payments, long saga timeout.</summary>
public sealed class DefaultSystem() : SystemFixture(new SystemOptions());

/// <summary>Payments take longer than the saga is willing to wait, to exercise timeouts and late-result compensation.</summary>
public sealed class SlowPaymentSystem() : SystemFixture(new SystemOptions { PaymentLatencyMs = 3500, SagaPaymentTimeoutSeconds = 2 });

/// <summary>The customer has to enter card details: the payment waits, and a 12 s saga timeout ends orders nobody pays for.</summary>
public sealed class CardPaymentSystem() : SystemFixture(new SystemOptions { SagaPaymentTimeoutSeconds = 12, CustomerPaysByCard = true });
