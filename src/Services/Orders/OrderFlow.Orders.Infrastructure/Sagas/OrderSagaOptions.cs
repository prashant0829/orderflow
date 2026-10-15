namespace OrderFlow.Orders.Infrastructure.Sagas;

public class OrderSagaOptions
{
    public const string SectionName = "OrderSaga";

    /// <summary>How long we wait for the payment result before giving up and undoing the order. With a card form the customer needs minutes, so the default is 15.</summary>
    public int PaymentTimeoutSeconds { get; set; } = 900;
}
