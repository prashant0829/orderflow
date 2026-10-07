using FluentAssertions;
using NSubstitute;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Commands;
using OrderFlow.Orders.Domain.Orders;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.UnitTests.Application;

public class CreateOrderHandlerTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IProductCatalog _catalog = Substitute.For<IProductCatalog>();
    private readonly CreateOrderCommandHandler _handler;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly CreateOrderCommand Command = new(CustomerId, "key-1", [new OrderLineInput(ProductId, 2)]);

    public CreateOrderHandlerTests()
    {
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ProductInfo> { [ProductId] = new(ProductId, "Keyboard", 10m) });
        _handler = new CreateOrderCommandHandler(_repository, _catalog, _unitOfWork, _cache);
    }

    [Fact]
    public async Task Handle_UsesCatalogPriceNotClientPrice()
    {
        var result = await _handler.Handle(Command, CancellationToken.None);

        result.Order.TotalAmount.Should().Be(20m);
        result.Order.Lines.Should().ContainSingle().Which.UnitPrice.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFoundAndSavesNothing()
    {
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ProductInfo>());

        var act = () => _handler.Handle(Command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewKey_SavesOrderAndCachesIdempotencyKey()
    {
        var result = await _handler.Handle(Command, CancellationToken.None);

        result.WasCreated.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(CacheKeys.Idempotency(CustomerId, "key-1"), Arg.Any<Guid>(), CacheDurations.Idempotency, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KeyAlreadyInDatabase_ReturnsExistingWithoutSaving()
    {
        var existing = Order.Create(CustomerId, "key-1", [(Guid.NewGuid(), 1, 3m)]);
        _repository.GetByIdempotencyKeyAsync(CustomerId, "key-1", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(Command, CancellationToken.None);

        result.WasCreated.Should().BeFalse();
        result.Order.Id.Should().Be(existing.Id);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LosesRaceOnUniqueIndex_ReturnsWinnersOrder()
    {
        var winner = Order.Create(CustomerId, "key-1", [(Guid.NewGuid(), 1, 3m)]);
        _repository.GetByIdempotencyKeyAsync(CustomerId, "key-1", Arg.Any<CancellationToken>())
            .Returns((Order?)null, winner);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<int>(_ => throw new DuplicateRequestException());

        var result = await _handler.Handle(Command, CancellationToken.None);

        result.WasCreated.Should().BeFalse();
        result.Order.Id.Should().Be(winner.Id);
    }
}
