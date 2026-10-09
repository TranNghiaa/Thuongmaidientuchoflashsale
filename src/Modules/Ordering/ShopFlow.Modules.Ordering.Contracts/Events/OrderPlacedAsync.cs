namespace ShopFlow.Modules.Ordering.Contracts.Events;

public record OrderPlacedAsyncItem(string SkuId, int Quantity);
public record OrderPlacedAsync(Guid UserId, List<OrderPlacedAsyncItem> Items);
