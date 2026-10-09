using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Catalog.Contracts;
using ShopFlow.Modules.Catalog.Infrastructure;

namespace ShopFlow.Modules.Catalog.Application;

internal class CatalogApi : ICatalogApi
{
    private readonly CatalogDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CatalogApi(CatalogDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<decimal?> GetEffectivePriceAsync(string skuId, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SkuId == skuId, cancellationToken);

        if (product == null) return null;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var activeFlashSale = await _dbContext.FlashSales
            .AsNoTracking()
            .Where(fs => fs.SkuId == skuId && fs.StartsAt <= now && fs.EndsAt > now)
            .OrderBy(fs => fs.SalePrice)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeFlashSale != null)
        {
            return activeFlashSale.SalePrice;
        }

        return product.BasePrice;
    }
}
