using OrderFlow.Inventory.Domain.Reservations;

namespace OrderFlow.Inventory.Application.Abstractions;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);

    Task<Reservation?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
