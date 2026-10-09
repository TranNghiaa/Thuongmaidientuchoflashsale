using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Catalog.Infrastructure;

internal class CatalogMigrator : IModuleMigrator
{
    private readonly CatalogDbContext _dbContext;
    private readonly ILogger<CatalogMigrator> _logger;

    public CatalogMigrator(CatalogDbContext dbContext, ILogger<CatalogMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Schema => "catalog";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Migrating catalog schema...");
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {Schema};", cancellationToken: ct);
#pragma warning restore EF1002
        await _dbContext.Database.MigrateAsync(cancellationToken: ct);
        _logger.LogInformation("Catalog schema migrated.");
    }
}
