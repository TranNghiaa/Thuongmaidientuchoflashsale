namespace ShopFlow.Modules.Catalog.Contracts;

public interface ICatalogApi
{
    Task<decimal?> GetEffectivePriceAsync(string skuId, CancellationToken cancellationToken = default);
}
