using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;
using ShopFlow.BuildingBlocks.Database;

namespace ShopFlow.IntegrationTests;

public class ShopFlowApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
#pragma warning disable CS0618
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();
#pragma warning restore CS0618

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var migrators = scope.ServiceProvider.GetServices<IModuleMigrator>();
        foreach (var migrator in migrators)
        {
            await migrator.MigrateAsync(CancellationToken.None);
        }
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _dbContainer.GetConnectionString());
        builder.UseSetting("Messaging:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "IntegrationTestSecretMustBe32CharactersOrMore!");
        builder.UseSetting("Jwt:Issuer", "IntegrationTest");
        builder.UseSetting("Jwt:Audience", "IntegrationTest");
        
        builder.UseEnvironment("IntegrationTesting");
    }
}
