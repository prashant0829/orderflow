using MassTransit;
using MediatR;
using OrderFlow.Contracts.Inventory;
using OrderFlow.Inventory.Application.Reservations.Commands;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public class ReserveStockConsumer(ISender sender) : IConsumer<ReserveStock>
{
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        var message = context.Message;
        var lines = message.Lines.Select(line => new ReservationLineInput(line.ProductId, line.Quantity)).ToList();

        var result = await sender.Send(new ReserveStockCommand(message.OrderId, lines), context.CancellationToken);

        if (result.Success)
        {
            await context.Publish(new StockReserved(message.OrderId));
        }
        else
        {
            await context.Publish(new StockReservationFailed(message.OrderId, result.FailureReason ?? "Unknown reason."));
        }
    }
}
