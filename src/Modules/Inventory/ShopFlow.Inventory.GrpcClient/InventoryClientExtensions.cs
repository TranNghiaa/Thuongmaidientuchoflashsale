using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopFlow.Modules.Inventory.Contracts;

namespace ShopFlow.Inventory.GrpcClient;

/// <summary>
/// Extension to register inventory in dual-mode: InProcess or Grpc.
/// T30: Inventory:Mode controls which implementation is used.
/// </summary>
public static class InventoryClientExtensions
{
    /// <summary>
    /// Registers IInventoryApi as gRPC client when Inventory:Mode=Grpc.
    /// This replaces the in-process registration from AddInventoryModule.
    /// </summary>
    public static IServiceCollection AddInventoryGrpcClient(this IServiceCollection services, IConfiguration configuration)
    {
        var grpcAddress = configuration["Inventory:GrpcAddress"]
            ?? throw new InvalidOperationException("Inventory:GrpcAddress must be configured when Inventory:Mode=Grpc");

        services.AddSingleton<IInventoryApi>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<InventoryGrpcClient>>();
            return new InventoryGrpcClient(grpcAddress, logger);
        });

        return services;
    }
}
