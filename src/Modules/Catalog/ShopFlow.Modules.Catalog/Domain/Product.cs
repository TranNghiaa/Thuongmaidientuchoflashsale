namespace ShopFlow.Modules.Catalog.Domain;

internal class Product
{
    public Guid Id { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
