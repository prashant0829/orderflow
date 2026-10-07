using FluentAssertions;
using NSubstitute;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Orders.Queries;
using OrderFlow.Orders.Domain.Orders;

namespace OrderFlow.Orders.UnitTests.Application;

public class ListAllOrdersTests
{
    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task Handle_PassesParsedStatusAndPagingToRepository()
    {
        var order = Order.Create(Guid.NewGuid(), "key", [(Guid.NewGuid(), 1, 5m)]);
        _repository.ListAllAsync(OrderStatus.Failed, 2, 10, Arg.Any<CancellationToken>()).Returns(([order], 11));

        var result = await new ListAllOrdersQueryHandler(_repository).Handle(new ListAllOrdersQuery("failed", 2, 10), CancellationToken.None);

        result.TotalCount.Should().Be(11);
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_NoStatus_ListsEverything()
    {
        _repository.ListAllAsync(null, 1, 20, Arg.Any<CancellationToken>()).Returns(([], 0));

        var result = await new ListAllOrdersQueryHandler(_repository).Handle(new ListAllOrdersQuery(null), CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Banana", false)]
    [InlineData("Confirmed", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Validator_OnlyAcceptsRealStatuses(string? status, bool valid)
    {
        new ListAllOrdersQueryValidator().Validate(new ListAllOrdersQuery(status)).IsValid.Should().Be(valid);
    }
}
