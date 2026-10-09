using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Ordering.Infrastructure;

internal class OrderingMigrator : IModuleMigrator
{
    private readonly OrderingDbContext _dbContext;
    private readonly ILogger<OrderingMigrator> _logger;

    public OrderingMigrator(OrderingDbContext dbContext, ILogger<OrderingMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Schema => "ordering";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Migrating ordering schema...");
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {Schema};", cancellationToken: ct);
#pragma warning restore EF1002
        await _dbContext.Database.MigrateAsync(cancellationToken: ct);
        _logger.LogInformation("Ordering schema migrated.");
    }
}
