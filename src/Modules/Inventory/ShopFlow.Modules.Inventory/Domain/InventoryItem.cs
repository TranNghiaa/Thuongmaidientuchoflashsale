namespace ShopFlow.Modules.Inventory.Domain;

internal class InventoryItem
{
    public Guid Id { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
