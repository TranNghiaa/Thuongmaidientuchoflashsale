using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Identity.Infrastructure;

internal class IdentityMigrator : IModuleMigrator
{
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<IdentityMigrator> _logger;

    public IdentityMigrator(IdentityDbContext dbContext, ILogger<IdentityMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Schema => "identity";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Migrating identity schema...");
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {Schema};", cancellationToken: ct);
#pragma warning restore EF1002
        await _dbContext.Database.MigrateAsync(cancellationToken: ct);
        _logger.LogInformation("Identity schema migrated.");
    }
}
