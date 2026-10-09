using Microsoft.Extensions.DependencyInjection;
using ShopFlow.Modules.Inventory.Contracts;
using Xunit;

namespace ShopFlow.IntegrationTests.Inventory;

[Collection("Integration Tests")]
public class InventoryContractTests : IClassFixture<ShopFlowApplicationFactory>
{
    private readonly ShopFlowApplicationFactory _factory;

    public InventoryContractTests(ShopFlowApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void IInventoryApi_Should_Be_Registered_And_Resolvable()
    {
        // Act
        using var scope = _factory.Services.CreateScope();
        var api = scope.ServiceProvider.GetService<IInventoryApi>();

        // Assert
        Assert.NotNull(api);
        // By default it should be InventoryApi (InProcess mode)
        Assert.Equal("ShopFlow.Modules.Inventory.Application.InventoryApi", api.GetType().FullName);
    }
}
