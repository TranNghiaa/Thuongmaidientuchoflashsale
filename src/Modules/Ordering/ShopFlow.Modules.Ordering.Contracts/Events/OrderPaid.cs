namespace ShopFlow.Modules.Ordering.Contracts.Events;

public record OrderPaid(Guid OrderId, Guid UserId, decimal TotalAmount);
