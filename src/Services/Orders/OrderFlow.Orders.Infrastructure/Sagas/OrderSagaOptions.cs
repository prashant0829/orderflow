namespace OrderFlow.Orders.Infrastructure.Sagas;

public class OrderSagaOptions
{
    public const string SectionName = "OrderSaga";

    /// <summary>How long we wait for the payment result before giving up and undoing the order.</summary>
    public int PaymentTimeoutSeconds { get; set; } = 60;
}
