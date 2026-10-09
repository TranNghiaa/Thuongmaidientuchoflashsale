namespace ShopFlow.BuildingBlocks.Database;

public interface IModuleMigrator
{
    string Schema { get; }
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public interface IModuleSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
