namespace OrderFlow.Orders.Infrastructure.Catalog;

public class InventoryCatalogOptions
{
    public const string SectionName = "Services:Inventory";

    public string BaseUrl { get; set; } = "http://localhost:5102";
}
