using MassTransit;
using MediatR;
using OrderFlow.Contracts.Inventory;
using OrderFlow.Inventory.Application.Reservations.Commands;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public class ReleaseStockConsumer(ISender sender) : IConsumer<ReleaseStock>
{
    public Task Consume(ConsumeContext<ReleaseStock> context) =>
        sender.Send(new ReleaseStockCommand(context.Message.OrderId), context.CancellationToken);
}
