using OrderFlow.SharedKernel;
using OrderFlow.Orders.Domain.Common;

namespace OrderFlow.Orders.Domain.Orders;

public class OrderLine
{
    private OrderLine()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;

    internal static OrderLine Create(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product id is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.");
        }

        return new OrderLine { Id = Guid.NewGuid(), ProductId = productId, Quantity = quantity, UnitPrice = unitPrice };
    }
}
