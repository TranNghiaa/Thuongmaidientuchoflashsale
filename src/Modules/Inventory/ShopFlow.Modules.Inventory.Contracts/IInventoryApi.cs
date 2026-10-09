namespace ShopFlow.Modules.Inventory.Contracts;

public class ReserveItemDto
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public interface IInventoryApi
{
    Task<bool> ReserveAsync(string referenceId, IEnumerable<ReserveItemDto> items, CancellationToken cancellationToken = default);
    Task<bool> CommitAsync(string referenceId, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(string referenceId, CancellationToken cancellationToken = default);
}
