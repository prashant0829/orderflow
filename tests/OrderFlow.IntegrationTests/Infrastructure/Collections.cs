using Xunit;

namespace OrderFlow.IntegrationTests.Infrastructure;

// Collections share one running system each.
[CollectionDefinition(Name)]
public sealed class DefaultSystemCollection : ICollectionFixture<DefaultSystem>
{
    public const string Name = "default-system";
}

[CollectionDefinition(Name)]
public sealed class SlowPaymentCollection : ICollectionFixture<SlowPaymentSystem>
{
    public const string Name = "slow-payment-system";
}
