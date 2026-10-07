using OrderFlow.SharedKernel;
using FluentAssertions;
using NSubstitute;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Commands;
using OrderFlow.Orders.Application.Orders.Dtos;
using OrderFlow.Orders.Application.Orders.Queries;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.UnitTests.Application;

public class QueryAndCancelTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private static Order NewOrder() => Order.Create(Guid.NewGuid(), "key", [(Guid.NewGuid(), 1, 4m)]);

    [Fact]
    public async Task GetOrder_CacheHit_SkipsDatabase()
    {
        var order = NewOrder();
        _cache.GetAsync<OrderDto>(CacheKeys.Order(order.Id), Arg.Any<CancellationToken>()).Returns(order.ToDto());

        var result = await new GetOrderQueryHandler(_repository, _cache).Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        result.Id.Should().Be(order.Id);
        await _repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrder_CacheMiss_LoadsFromDatabaseAndPopulatesCache()
    {
        var order = NewOrder();
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await new GetOrderQueryHandler(_repository, _cache).Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        await _cache.Received(1).SetAsync(CacheKeys.Order(order.Id), Arg.Any<OrderDto>(), CacheDurations.Order, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrder_Unknown_ThrowsNotFound()
    {
        var act = () => new GetOrderQueryHandler(_repository, _cache).Handle(new GetOrderQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelOrder_InvalidatesCache()
    {
        var order = NewOrder();
        _repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await new CancelOrderCommandHandler(_repository, _unitOfWork, _cache)
            .Handle(new CancelOrderCommand(order.Id, "reason"), CancellationToken.None);

        result.Status.Should().Be(nameof(OrderStatus.Cancelled));
        await _cache.Received(1).RemoveAsync(CacheKeys.Order(order.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreateOrderValidator_RejectsEmptyLinesAndMissingKey()
    {
        var result = new CreateOrderCommandValidator().Validate(new CreateOrderCommand(Guid.NewGuid(), "", []));

        result.Errors.Select(error => error.PropertyName).Should().Contain(["IdempotencyKey", "Lines"]);
    }
}
