using ShopFlow.Modules.Catalog.Contracts;

namespace ShopFlow.Modules.Ordering.Application;

internal class OrderPricingItem
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

internal class OrderPricingResult
{
    public decimal TotalAmount { get; set; }
    public List<OrderPricingResultItem> Items { get; set; } = new();
}

internal class OrderPricingResultItem
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

internal class OrderPricingService
{
    private readonly ICatalogApi _catalogApi;

    public OrderPricingService(ICatalogApi catalogApi)
    {
        _catalogApi = catalogApi;
    }

    public async Task<OrderPricingResult> CalculatePricingAsync(IEnumerable<OrderPricingItem> items, CancellationToken ct = default)
    {
        var result = new OrderPricingResult();

        foreach (var item in items)
        {
            var price = await _catalogApi.GetEffectivePriceAsync(item.SkuId, ct);
            if (price == null)
            {
                throw new InvalidOperationException($"SKU not found or price unavailable: {item.SkuId}");
            }

            var totalPrice = price.Value * item.Quantity;
            result.Items.Add(new OrderPricingResultItem
            {
                SkuId = item.SkuId,
                Quantity = item.Quantity,
                UnitPrice = price.Value,
                TotalPrice = totalPrice
            });

            result.TotalAmount += totalPrice;
        }

        return result;
    }
}
