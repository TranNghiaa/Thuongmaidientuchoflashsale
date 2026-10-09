namespace ShopFlow.Modules.Payment.Domain;

internal class PaymentTransaction
{
    public Guid Id { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}
