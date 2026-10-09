using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Inventory.Infrastructure;

internal class InventoryMigrator : IModuleMigrator
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<InventoryMigrator> _logger;

    public InventoryMigrator(InventoryDbContext dbContext, ILogger<InventoryMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Schema => "inventory";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Migrating inventory schema...");
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {Schema};", cancellationToken: ct);
#pragma warning restore EF1002
        await _dbContext.Database.MigrateAsync(cancellationToken: ct);
        _logger.LogInformation("Inventory schema migrated.");
    }
}
