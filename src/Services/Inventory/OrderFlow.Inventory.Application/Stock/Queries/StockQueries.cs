using MediatR;
using OrderFlow.Caching;
using OrderFlow.Inventory.Application.Abstractions;
using OrderFlow.Inventory.Application.Common;
using OrderFlow.Inventory.Application.Stock.Dtos;
using OrderFlow.SharedKernel;

namespace OrderFlow.Inventory.Application.Stock.Queries;

public record ListStockQuery : IRequest<IReadOnlyList<StockItemDto>>;

public record GetStockItemQuery(Guid ProductId) : IRequest<StockItemDto>;

public class ListStockQueryHandler(IStockRepository repository, ICacheService cache)
    : IRequestHandler<ListStockQuery, IReadOnlyList<StockItemDto>>
{
    public async Task<IReadOnlyList<StockItemDto>> Handle(ListStockQuery request, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync<List<StockItemDto>>(CacheKeys.StockList, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var items = (await repository.ListAsync(cancellationToken)).Select(item => item.ToDto()).ToList();
        await cache.SetAsync(CacheKeys.StockList, items, CacheDurations.Stock, cancellationToken);
        return items;
    }
}

public class GetStockItemQueryHandler(IStockRepository repository, ICacheService cache)
    : IRequestHandler<GetStockItemQuery, StockItemDto>
{
    public async Task<StockItemDto> Handle(GetStockItemQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.StockItem(request.ProductId);

        var cached = await cache.GetAsync<StockItemDto>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var item = await repository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var dto = item.ToDto();
        await cache.SetAsync(key, dto, CacheDurations.Stock, cancellationToken);
        return dto;
    }
}
