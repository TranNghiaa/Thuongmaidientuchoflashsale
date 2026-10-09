using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.Modules.Ordering.Infrastructure;
using Xunit;
using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Inventory.Infrastructure;
using ShopFlow.Modules.Catalog.Infrastructure;
using System.Net.Http.Headers;

namespace ShopFlow.IntegrationTests.Ordering;

[Collection("Integration Tests")]
public class OrderingTests : IClassFixture<ShopFlowApplicationFactory>
{
    private readonly ShopFlowApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrderingTests(ShopFlowApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync(Guid userId)
    {
        // Mock a token for tests
        // Actually, we need to log in or configure the test server to allow mock auth
        // Or we can register a test user. Since Identity is real, we can call POST /auth/register
        var email = $"test{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/auth/register", new { Email = email, Password = "Password123!", FullName = "Test User" });
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new { Email = email, Password = "Password123!" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return loginResult.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task PlaceOrder_Success()
    {
        var skuId = Guid.NewGuid().ToString("N").Substring(0, 10);
        var userId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            catalogDb.Products.Add(new ShopFlow.Modules.Catalog.Domain.Product
            {
                Id = Guid.NewGuid(),
                SkuId = skuId,
                Name = "Test Product",
                BasePrice = 1000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await catalogDb.SaveChangesAsync();

            var invDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            invDb.InventoryItems.Add(new ShopFlow.Modules.Inventory.Domain.InventoryItem
            {
                Id = Guid.NewGuid(),
                SkuId = skuId,
                AvailableQuantity = 10,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow
            });
            await invDb.SaveChangesAsync();
        }

        var token = await GetAuthTokenAsync(userId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Items = new[]
            {
                new { SkuId = skuId, Quantity = 2 }
            }
        };

        var response = await _client.PostAsJsonAsync("/orders", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orderId = json.GetProperty("id").GetGuid();
        var status = json.GetProperty("status").GetString();

        Assert.Equal("Paid", status);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
            var order = await db.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal("Paid", order.Status);
            Assert.Equal(2000, order.TotalAmount);
        }
    }

    [Fact]
    public async Task PlaceOrder_Shortage_Returns400()
    {
        var skuId = Guid.NewGuid().ToString("N").Substring(0, 10);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            catalogDb.Products.Add(new ShopFlow.Modules.Catalog.Domain.Product
            {
                Id = Guid.NewGuid(),
                SkuId = skuId,
                Name = "Test Product",
                BasePrice = 1000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await catalogDb.SaveChangesAsync();

            var invDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            invDb.InventoryItems.Add(new ShopFlow.Modules.Inventory.Domain.InventoryItem
            {
                Id = Guid.NewGuid(),
                SkuId = skuId,
                AvailableQuantity = 1,
                ReservedQuantity = 0,
                CreatedAt = DateTime.UtcNow
            });
            await invDb.SaveChangesAsync();
        }

        var token = await GetAuthTokenAsync(Guid.NewGuid());
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Items = new[]
            {
                new { SkuId = skuId, Quantity = 5 } // Request more than available
            }
        };

        var response = await _client.PostAsJsonAsync("/orders", request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
