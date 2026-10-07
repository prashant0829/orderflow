using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Inventory.Application.Stock.Commands;
using OrderFlow.Inventory.Application.Stock.Dtos;
using OrderFlow.Inventory.Application.Stock.Queries;
using OrderFlow.Security;

namespace OrderFlow.Inventory.Api.Controllers;

public record CreateStockItemRequest(string Name, decimal UnitPrice, int InitialQuantity);

public record RestockRequest(int Quantity);

public record LookupStockRequest(IReadOnlyCollection<Guid> ProductIds);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock")]
public class StockController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListStockQuery(), cancellationToken));

    [HttpGet("{productId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<StockItemDto>> Get(Guid productId, CancellationToken cancellationToken) =>
        await sender.Send(new GetStockItemQuery(productId), cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public async Task<ActionResult<StockItemDto>> Create([FromBody] CreateStockItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new CreateStockItemCommand(request.Name, request.UnitPrice, request.InitialQuantity), cancellationToken);
        return CreatedAtAction(nameof(Get), new { productId = item.ProductId, version = "1.0" }, item);
    }

    [HttpPost("lookup")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> Lookup([FromBody] LookupStockRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new LookupStockQuery(request.ProductIds), cancellationToken));

    [HttpPost("{productId:guid}/restock")]
    [Authorize(Policy = PolicyNames.AdminOnly)]
    public async Task<ActionResult<StockItemDto>> Restock(Guid productId, [FromBody] RestockRequest request, CancellationToken cancellationToken) =>
        await sender.Send(new RestockCommand(productId, request.Quantity), cancellationToken);
}
