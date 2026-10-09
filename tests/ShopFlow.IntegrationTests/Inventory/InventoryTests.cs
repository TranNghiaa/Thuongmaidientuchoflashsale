using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.Modules.Inventory.Infrastructure;
using ShopFlow.Modules.Inventory.Domain;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace ShopFlow.IntegrationTests.Inventory;

[Collection("Integration Tests")]
public class InventoryTests : IClassFixture<ShopFlowApplicationFactory>
{
    private readonly ShopFlowApplicationFactory _factory;
    private readonly HttpClient _client;

    public InventoryTests(ShopFlowApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Concurrent_Reservations_Should_Prevent_Overselling()
    {
        var skuId = Guid.NewGuid().ToString("N");
        var initialQuantity = 5;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            db.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                SkuId = skuId,
                AvailableQuantity = initialQuantity,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            var request = new
            {
                ReferenceId = Guid.NewGuid().ToString(),
                Items = new[]
                {
                    new { SkuId = skuId, Quantity = 1 }
                }
            };
            tasks.Add(_client.PostAsJsonAsync("/inventory/reserve", request));
        }

        var responses = await Task.WhenAll(tasks);
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        var failureCount = responses.Count(r => !r.IsSuccessStatusCode);

        Assert.Equal(5, successCount);
        Assert.Equal(5, failureCount);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var item = await db.InventoryItems.FirstOrDefaultAsync(x => x.SkuId == skuId);
            Assert.NotNull(item);
            Assert.Equal(0, item.AvailableQuantity);
            Assert.Equal(5, item.ReservedQuantity);
        }
    }
}
