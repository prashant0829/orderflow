using FluentAssertions;
using OrderFlow.Inventory.Domain.Stock;
using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.UnitTests;

public class StockItemTests
{
    private static StockItem NewItem(int quantity = 10) => StockItem.Create(Guid.NewGuid(), "Keyboard", 50m, quantity);

    [Fact]
    public void Reserve_ReducesAvailableQuantity()
    {
        var item = NewItem();

        item.Reserve(4);

        item.AvailableQuantity.Should().Be(6);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_Throws()
    {
        var act = () => NewItem(2).Reserve(3);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Release_AddsQuantityBack()
    {
        var item = NewItem();
        item.Reserve(4);

        item.Release(4);

        item.AvailableQuantity.Should().Be(10);
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("Name", -1, 1)]
    [InlineData("Name", 1, -1)]
    public void Create_WithInvalidValues_Throws(string name, double price, int quantity)
    {
        var act = () => StockItem.Create(Guid.NewGuid(), name, (decimal)price, quantity);

        act.Should().Throw<DomainException>();
    }
}
