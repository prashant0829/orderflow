using FluentValidation;
using MediatR;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Stock.Dtos;

namespace OrderFlow.Inventory.Application.Stock.Queries;

/// <summary>Batch lookup used by other services (Orders) to get authoritative prices. Always reads the database.</summary>
public record LookupStockQuery(IReadOnlyCollection<Guid> ProductIds) : IRequest<IReadOnlyList<StockItemDto>>;

public class LookupStockQueryValidator : AbstractValidator<LookupStockQuery>
{
    private const int MaxProducts = 100;

    public LookupStockQueryValidator() =>
        RuleFor(query => query.ProductIds).NotEmpty().Must(ids => ids.Count <= MaxProducts)
            .WithMessage($"At most {MaxProducts} products per lookup.");
}

public class LookupStockQueryHandler(IStockRepository repository) : IRequestHandler<LookupStockQuery, IReadOnlyList<StockItemDto>>
{
    public async Task<IReadOnlyList<StockItemDto>> Handle(LookupStockQuery request, CancellationToken cancellationToken) =>
        (await repository.LookupAsync(request.ProductIds, cancellationToken)).Select(item => item.ToDto()).ToList();
}
