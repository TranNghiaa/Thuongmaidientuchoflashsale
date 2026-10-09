namespace ShopFlow.Modules.Inventory.Domain;

internal class Reservation
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Committed, Released
    
    public ICollection<ReservationItem> Items { get; set; } = new List<ReservationItem>();
}

internal class ReservationItem
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
