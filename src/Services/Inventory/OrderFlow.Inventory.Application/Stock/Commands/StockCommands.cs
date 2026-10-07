using FluentValidation;
using MediatR;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Common;
using OrderFlow.Inventory.Application.Stock.Dtos;
using OrderFlow.Inventory.Domain.Stock;
using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.Application.Stock.Commands;

public record CreateStockItemCommand(string Name, decimal UnitPrice, int InitialQuantity) : IRequest<StockItemDto>;

public record RestockCommand(Guid ProductId, int Quantity) : IRequest<StockItemDto>;

public class CreateStockItemCommandValidator : AbstractValidator<CreateStockItemCommand>
{
    private const int MaxNameLength = 200;

    public CreateStockItemCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.InitialQuantity).GreaterThanOrEqualTo(0);
    }
}

public class RestockCommandValidator : AbstractValidator<RestockCommand>
{
    public RestockCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Quantity).GreaterThan(0);
    }
}

public class CreateStockItemCommandHandler(IStockRepository repository, IUnitOfWork unitOfWork, ICacheService cache)
    : IRequestHandler<CreateStockItemCommand, StockItemDto>
{
    public async Task<StockItemDto> Handle(CreateStockItemCommand command, CancellationToken cancellationToken)
    {
        var item = StockItem.Create(Guid.Empty, command.Name, command.UnitPrice, command.InitialQuantity);

        await repository.AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync([item.Id], cancellationToken);

        return item.ToDto();
    }
}

public class RestockCommandHandler(IStockRepository repository, IUnitOfWork unitOfWork, ICacheService cache)
    : IRequestHandler<RestockCommand, StockItemDto>
{
    public async Task<StockItemDto> Handle(RestockCommand command, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        item.Restock(command.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync([item.Id], cancellationToken);

        return item.ToDto();
    }
}
