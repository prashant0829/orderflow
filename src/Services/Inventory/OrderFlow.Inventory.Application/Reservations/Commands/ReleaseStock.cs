using MediatR;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Common;
using OrderFlow.Inventory.Domain.Reservations;

namespace OrderFlow.Inventory.Application.Reservations.Commands;

public record ReleaseStockCommand(Guid OrderId) : IRequest;

/// <summary>Compensation for ReserveStock. Does nothing if there is no active reservation, so duplicates are harmless.</summary>
public class ReleaseStockCommandHandler(
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : IRequestHandler<ReleaseStockCommand>
{
    public async Task Handle(ReleaseStockCommand command, CancellationToken cancellationToken)
    {
        var reservation = await reservationRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (reservation is null || reservation.Status == ReservationStatus.Released)
        {
            return;
        }

        // Same product-id order as reserving, so releases and reservations can never deadlock each other.
        foreach (var line in reservation.Lines.OrderBy(line => line.ProductId))
        {
            await stockRepository.ReturnAsync(line.ProductId, line.Quantity, cancellationToken);
        }

        reservation.MarkReleased();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(reservation.Lines.Select(line => line.ProductId), cancellationToken);
        InventoryTelemetry.RecordRelease();
    }
}
