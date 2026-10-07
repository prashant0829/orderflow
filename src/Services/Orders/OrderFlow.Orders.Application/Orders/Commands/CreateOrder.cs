using FluentValidation;
using MediatR;
using OrderFlow.Caching;
using OrderFlow.Orders.Application.Abstractions;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Orders.Dtos;
using OrderFlow.Orders.Domain.Orders;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands;

public record OrderLineInput(Guid ProductId, int Quantity);

public record CreateOrderCommand(Guid CustomerId, string IdempotencyKey, IReadOnlyList<OrderLineInput> Lines)
    : IRequest<CreateOrderResult>;

public record CreateOrderResult(OrderDto Order, bool WasCreated);

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    private const int MaxIdempotencyKeyLength = 100;
    private const int MaxLinesPerOrder = 50;

    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(MaxIdempotencyKeyLength);
        RuleFor(command => command.Lines).NotEmpty().Must(lines => lines.Count <= MaxLinesPerOrder)
            .WithMessage($"An order can have at most {MaxLinesPerOrder} lines.");
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ProductId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0);
        });
    }
}

public class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IProductCatalog productCatalog,
    IUnitOfWork unitOfWork,
    ICacheService cache) : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var idempotencyCacheKey = CacheKeys.Idempotency(command.CustomerId, command.IdempotencyKey);

        var existingOrder = await FindExistingOrderAsync(command, idempotencyCacheKey, cancellationToken);
        if (existingOrder is not null)
        {
            return new CreateOrderResult(existingOrder.ToDto(), WasCreated: false);
        }

        var prices = await LoadPricesAsync(command, cancellationToken);

        var order = Order.Create(
            command.CustomerId,
            command.IdempotencyKey,
            command.Lines.Select(line => (line.ProductId, line.Quantity, prices[line.ProductId])));

        await orderRepository.AddAsync(order, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRequestException)
        {
            // A parallel request with the same key won the race; return its order instead.
            var winner = await orderRepository.GetByIdempotencyKeyAsync(command.CustomerId, command.IdempotencyKey, cancellationToken)
                ?? throw new DuplicateRequestException("Duplicate request, but the original order could not be found.");
            return new CreateOrderResult(winner.ToDto(), WasCreated: false);
        }

        await cache.SetAsync(idempotencyCacheKey, order.Id, CacheDurations.Idempotency, cancellationToken);

        OrdersTelemetry.RecordCreated();
        System.Diagnostics.Activity.Current?.SetTag("order.id", order.Id);

        return new CreateOrderResult(order.ToDto(), WasCreated: true);
    }

    /// <summary>Prices always come from Inventory, never from the client.</summary>
    private async Task<Dictionary<Guid, decimal>> LoadPricesAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var productIds = command.Lines.Select(line => line.ProductId).Distinct().ToList();
        var products = await productCatalog.GetProductsAsync(productIds, cancellationToken);

        var unknown = productIds.Where(productId => !products.ContainsKey(productId)).ToList();
        if (unknown.Count > 0)
        {
            throw new NotFoundException("Product", string.Join(", ", unknown));
        }

        return products.ToDictionary(pair => pair.Key, pair => pair.Value.UnitPrice);
    }

    private async Task<Order?> FindExistingOrderAsync(
        CreateOrderCommand command, string idempotencyCacheKey, CancellationToken cancellationToken)
    {
        var cachedOrderId = await cache.GetAsync<Guid?>(idempotencyCacheKey, cancellationToken);
        if (cachedOrderId is { } orderId)
        {
            var cachedOrder = await orderRepository.GetByIdAsync(orderId, cancellationToken);
            if (cachedOrder is not null)
            {
                return cachedOrder;
            }
        }

        return await orderRepository.GetByIdempotencyKeyAsync(command.CustomerId, command.IdempotencyKey, cancellationToken);
    }
}
