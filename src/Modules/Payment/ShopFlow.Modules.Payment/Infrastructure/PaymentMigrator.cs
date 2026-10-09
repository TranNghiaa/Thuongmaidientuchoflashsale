using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.Modules.Payment.Infrastructure;

internal class PaymentMigrator : IModuleMigrator
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<PaymentMigrator> _logger;

    public PaymentMigrator(PaymentDbContext dbContext, ILogger<PaymentMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public string Schema => "payment";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Migrating payment schema...");
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {Schema};", cancellationToken: ct);
#pragma warning restore EF1002
        await _dbContext.Database.MigrateAsync(cancellationToken: ct);
        _logger.LogInformation("Payment schema migrated.");
    }
}
