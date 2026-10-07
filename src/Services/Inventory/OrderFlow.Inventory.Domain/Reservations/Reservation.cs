using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.Domain.Reservations;

public class Reservation
{
    private readonly List<ReservationLine> _lines = [];

    private Reservation()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ReservationLine> Lines => _lines;

    public static Reservation Create(Guid orderId, IEnumerable<(Guid ProductId, int Quantity)> lines)
    {
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = ReservationStatus.Reserved,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var (productId, quantity) in lines)
        {
            reservation._lines.Add(ReservationLine.Create(productId, quantity));
        }

        if (reservation._lines.Count == 0)
        {
            throw new DomainException("A reservation needs at least one line.");
        }

        return reservation;
    }

    public void MarkReleased()
    {
        if (Status == ReservationStatus.Released)
        {
            throw new DomainException("Reservation is already released.");
        }

        Status = ReservationStatus.Released;
    }
}
