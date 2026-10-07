using FluentAssertions;
using NSubstitute;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Reservations.Commands;
using OrderFlow.Inventory.Domain.Reservations;
using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.UnitTests;

/// <summary>Behaves like the SQL version: "take" only succeeds when enough is left, and it is a single step.</summary>
internal sealed class FakeStockRepository : IStockRepository
{
    private readonly Dictionary<Guid, StockItem> _items = [];

    public List<Guid> TakeOrder { get; } = [];

    /// <summary>Runs just before a take, to simulate another order grabbing stock in between.</summary>
    public Action<Guid>? BeforeTake { get; set; }

    /// <summary>Another order empties the shelf.</summary>
    public void TakeEverything(Guid productId) => _items[productId].Reserve(_items[productId].AvailableQuantity);

    public StockItem Add(string name, int quantity)
    {
        var item = StockItem.Create(Guid.NewGuid(), name, 10m, quantity);
        _items[item.Id] = item;
        return item;
    }

    public Task AddAsync(StockItem item, CancellationToken cancellationToken)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<StockItem?> GetByIdAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(productId));

    public Task<IReadOnlyList<StockItem>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItem>>(productIds.Where(_items.ContainsKey).Select(id => _items[id]).ToList());

    public Task<IReadOnlyList<StockItem>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.ToList());

    public Task<bool> TryTakeAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        BeforeTake?.Invoke(productId);
        TakeOrder.Add(productId);
        var item = _items[productId];
        if (!item.CanReserve(quantity))
        {
            return Task.FromResult(false);
        }

        item.Reserve(quantity);
        return Task.FromResult(true);
    }

    public Task ReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        _items[productId].Release(quantity);
        return Task.CompletedTask;
    }
}

public class ReservationHandlerTests
{
    private readonly FakeStockRepository _stock = new();
    private readonly IReservationRepository _reservations = Substitute.For<IReservationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private ReserveStockCommandHandler ReserveHandler => new(_stock, _reservations, _unitOfWork, _cache);

    private ReleaseStockCommandHandler ReleaseHandler => new(_stock, _reservations, _unitOfWork, _cache);

    [Fact]
    public async Task Reserve_EnoughStock_TakesEveryLineAndSavesTheReservation()
    {
        var keyboard = _stock.Add("Keyboard", 10);
        var mouse = _stock.Add("Mouse", 2);

        var result = await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new(keyboard.Id, 3), new(mouse.Id, 1)]), CancellationToken.None);

        result.Success.Should().BeTrue();
        keyboard.AvailableQuantity.Should().Be(7);
        mouse.AvailableQuantity.Should().Be(1);
        await _reservations.Received(1).AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reserve_OneLineShort_TakesNothing()
    {
        var keyboard = _stock.Add("Keyboard", 10);
        var mouse = _stock.Add("Mouse", 2);

        var result = await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new(keyboard.Id, 3), new(mouse.Id, 5)]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Mouse").And.Contain("requested 5").And.Contain("available 2");
        keyboard.AvailableQuantity.Should().Be(10);
        _stock.TakeOrder.Should().BeEmpty("the early check rejects it before anything is written");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reserve_LastItemTakenByAnotherOrderInBetween_UndoesTheLinesAlreadyTaken()
    {
        var first = _stock.Add("Alpha", 10);
        var second = _stock.Add("Beta", 1);
        var lines = new[] { first.Id, second.Id }.OrderBy(id => id).ToList();
        // Whichever product is processed last gets emptied by "someone else" right before we try to take it.
        _stock.BeforeTake = productId =>
        {
            if (productId == lines[^1]) _stock.TakeEverything(productId);
        };

        var result = await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new(first.Id, 1), new(second.Id, 1)]), CancellationToken.None);

        result.Success.Should().BeFalse();
        var untouched = lines[0] == first.Id ? first : second;
        untouched.AvailableQuantity.Should().Be(lines[0] == first.Id ? 10 : 1, "the line taken before the failure is given back");
        await _reservations.DidNotReceive().AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reserve_UnknownProduct_Fails()
    {
        var keyboard = _stock.Add("Keyboard", 10);

        var result = await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new(keyboard.Id, 1), new(Guid.NewGuid(), 1)]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Unknown product");
        keyboard.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Reserve_SameProductOnTwoLines_SumsQuantities()
    {
        var keyboard = _stock.Add("Keyboard", 10);

        var result = await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new(keyboard.Id, 4), new(keyboard.Id, 5)]), CancellationToken.None);

        result.Success.Should().BeTrue();
        keyboard.AvailableQuantity.Should().Be(1);
    }

    [Fact]
    public async Task Reserve_AlwaysTakesProductsInIdOrder_SoTwoOrdersCanNeverDeadlock()
    {
        var items = Enumerable.Range(0, 5).Select(index => _stock.Add($"Item {index}", 10)).ToList();
        var shuffled = items.OrderByDescending(item => item.Id).Select(item => new ReservationLineInput(item.Id, 1)).ToList();

        await ReserveHandler.Handle(new ReserveStockCommand(Guid.NewGuid(), shuffled), CancellationToken.None);

        _stock.TakeOrder.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Reserve_DuplicateDelivery_ReturnsSuccessWithoutTakingAgain()
    {
        var keyboard = _stock.Add("Keyboard", 10);
        var orderId = Guid.NewGuid();
        _reservations.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(Reservation.Create(orderId, [(keyboard.Id, 3)]));

        var result = await ReserveHandler.Handle(new ReserveStockCommand(orderId, [new(keyboard.Id, 3)]), CancellationToken.None);

        result.Success.Should().BeTrue();
        keyboard.AvailableQuantity.Should().Be(10);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Release_ActiveReservation_ReturnsStockAndMarksReleased()
    {
        var keyboard = _stock.Add("Keyboard", 10);
        keyboard.Reserve(3);
        var orderId = Guid.NewGuid();
        var reservation = Reservation.Create(orderId, [(keyboard.Id, 3)]);
        _reservations.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(reservation);

        await ReleaseHandler.Handle(new ReleaseStockCommand(orderId), CancellationToken.None);

        keyboard.AvailableQuantity.Should().Be(10);
        reservation.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task Release_CalledTwice_OnlyRestocksOnce()
    {
        var keyboard = _stock.Add("Keyboard", 10);
        keyboard.Reserve(3);
        var orderId = Guid.NewGuid();
        _reservations.GetByOrderIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(Reservation.Create(orderId, [(keyboard.Id, 3)]));

        await ReleaseHandler.Handle(new ReleaseStockCommand(orderId), CancellationToken.None);
        await ReleaseHandler.Handle(new ReleaseStockCommand(orderId), CancellationToken.None);

        keyboard.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Release_NoReservation_DoesNothing()
    {
        await ReleaseHandler.Handle(new ReleaseStockCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
