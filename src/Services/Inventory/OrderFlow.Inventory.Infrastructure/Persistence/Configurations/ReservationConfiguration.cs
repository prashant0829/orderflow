using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Inventory.Domain.Reservations;

namespace OrderFlow.Inventory.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Id).ValueGeneratedNever();
        builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One reservation per order: a duplicate command cannot reserve twice, even if two run in parallel.
        builder.HasIndex(reservation => reservation.OrderId).IsUnique();

        builder.HasMany(reservation => reservation.Lines).WithOne().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(reservation => reservation.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ReservationLineConfiguration : IEntityTypeConfiguration<ReservationLine>
{
    public void Configure(EntityTypeBuilder<ReservationLine> builder)
    {
        builder.ToTable("ReservationLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
    }
}
