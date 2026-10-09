namespace ShopFlow.Modules.Ordering.Domain;

internal class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft, PendingPayment, Paid, Failed, Cancelled, Completed
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

internal class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
