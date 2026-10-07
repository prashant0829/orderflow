using MediatR;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Common;
using OrderFlow.Inventory.Domain.Reservations;
using OrderFlow.Inventory.Domain.Stock;

namespace OrderFlow.Inventory.Application.Reservations.Commands;

public record ReservationLineInput(Guid ProductId, int Quantity);

public record ReserveStockCommand(Guid OrderId, IReadOnlyList<ReservationLineInput> Lines) : IRequest<ReserveStockResult>;

public record ReserveStockResult(bool Success, string? FailureReason)
{
    public static ReserveStockResult Succeeded() => new(true, null);

    public static ReserveStockResult Failed(string reason) => new(false, reason);
}

/// <summary>
/// All-or-nothing: either every line is reserved or nothing changes. Safe to run twice for the same order.
/// Stock is taken with atomic conditional updates, always in product-id order. Two orders that share products therefore lock
/// rows in the same order (no deadlocks), and a popular product is a short queue on one row lock instead of a retry storm.
/// </summary>
public class ReserveStockCommandHandler(
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : IRequestHandler<ReserveStockCommand, ReserveStockResult>
{
    public async Task<ReserveStockResult> Handle(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        var existing = await reservationRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (existing is not null)
        {
            InventoryTelemetry.RecordReservation(InventoryTelemetry.Duplicate);
            return existing.Status == ReservationStatus.Reserved
                ? ReserveStockResult.Succeeded()
                : ReserveStockResult.Failed("Stock for this order was already released.");
        }

        var requested = command.Lines
            .GroupBy(line => line.ProductId)
            .OrderBy(group => group.Key)
            .Select(group => (ProductId: group.Key, Quantity: group.Sum(line => line.Quantity)))
            .ToList();

        // Cheap early exit that writes nothing; the real decision is made by the atomic updates below.
        var snapshot = await stockRepository.LookupAsync(requested.Select(line => line.ProductId).ToList(), cancellationToken);
        var failure = FindFailure(requested, snapshot);
        if (failure is not null)
        {
            InventoryTelemetry.RecordReservation(InventoryTelemetry.Rejected);
            return ReserveStockResult.Failed(failure);
        }

        var taken = new List<(Guid ProductId, int Quantity)>();
        foreach (var line in requested)
        {
            if (await stockRepository.TryTakeAsync(line.ProductId, line.Quantity, cancellationToken))
            {
                taken.Add(line);
                continue;
            }

            // Someone else took the last items between our check and our update. Undo what we already took in this attempt.
            foreach (var undo in taken)
            {
                await stockRepository.ReturnAsync(undo.ProductId, undo.Quantity, cancellationToken);
            }

            InventoryTelemetry.RecordReservation(InventoryTelemetry.Rejected);
            return ReserveStockResult.Failed(await DescribeShortageAsync(line, cancellationToken));
        }

        await reservationRepository.AddAsync(Reservation.Create(command.OrderId, requested), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(requested.Select(line => line.ProductId), cancellationToken);

        InventoryTelemetry.RecordReservation(InventoryTelemetry.Reserved);
        return ReserveStockResult.Succeeded();
    }

    private async Task<string> DescribeShortageAsync((Guid ProductId, int Quantity) line, CancellationToken cancellationToken)
    {
        var lookup = await stockRepository.LookupAsync([line.ProductId], cancellationToken);
        var current = lookup.Count > 0 ? lookup[0] : null;
        return current is null
            ? $"Unknown product(s): {line.ProductId}."
            : $"Insufficient stock: {current.Name} (requested {line.Quantity}, available {current.AvailableQuantity}).";
    }

    private static string? FindFailure(IReadOnlyList<(Guid ProductId, int Quantity)> requested, IReadOnlyList<StockItem> items)
    {
        var known = items.ToDictionary(item => item.Id);

        var unknown = requested.Where(line => !known.ContainsKey(line.ProductId)).Select(line => line.ProductId).ToList();
        if (unknown.Count > 0)
        {
            return $"Unknown product(s): {string.Join(", ", unknown)}.";
        }

        var shortages = requested
            .Where(line => !known[line.ProductId].CanReserve(line.Quantity))
            .Select(line => $"{known[line.ProductId].Name} (requested {line.Quantity}, available {known[line.ProductId].AvailableQuantity})")
            .ToList();

        return shortages.Count > 0 ? $"Insufficient stock: {string.Join("; ", shortages)}." : null;
    }
}
