namespace ShopFlow.Modules.Catalog.Domain;

internal class FlashSale
{
    public Guid Id { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}
