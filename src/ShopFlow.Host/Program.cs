using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using ShopFlow.BuildingBlocks;
using ShopFlow.BuildingBlocks.Auth;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.BuildingBlocks.Messaging;
using ShopFlow.Modules.Identity;
using ShopFlow.Modules.Catalog;
using ShopFlow.Modules.Inventory;
using ShopFlow.Inventory.GrpcClient;
using ShopFlow.Modules.Ordering;
using ShopFlow.Modules.Payment;
using ShopFlow.Modules.Notification;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(lo => lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2);
});

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    if (context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
    }
    else
    {
        configuration.WriteTo.Console();
    }
    
    configuration.ReadFrom.Configuration(context.Configuration);
    configuration.Enrich.FromLogContext();
});

// Validate configuration
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Default is required.");
}

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy())
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { "ready" })
    .AddS3(s3 => 
    {
        var accessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
        var secretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
        s3.Credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
        s3.S3Config = new Amazon.S3.AmazonS3Config 
        { 
            ServiceURL = builder.Configuration["Minio:Endpoint"] ?? "http://localhost:9000",
            ForcePathStyle = true
        };
        s3.BucketName = builder.Configuration["Minio:Bucket"] ?? "product-images";
    }, name: "minio", tags: new[] { "ready" });

// Add BuildingBlocks
builder.Services.AddBuildingBlocks();
builder.Services.AddShopFlowJwtAuth(builder.Configuration);

// Add Modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
// T30: Dual-mode Inventory — InProcess (default) or Grpc
var inventoryMode = builder.Configuration["Inventory:Mode"] ?? "InProcess";
if (inventoryMode.Equals("Grpc", StringComparison.OrdinalIgnoreCase))
{
    // In Grpc mode: do NOT register InventoryModule (no local DB, no sweeper)
    // Register gRPC client as IInventoryApi
    builder.Services.AddInventoryGrpcClient(builder.Configuration);
}
else
{
    // InProcess mode: use local InventoryModule
    builder.Services.AddInventoryModule(builder.Configuration);
}
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddPaymentModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);

// Add Message Bus
builder.Services.AddMessageBus(builder.Configuration);

// Add gRPC
builder.Services.AddGrpc();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// CLI Arguments
if (args.Contains("--healthcheck"))
{
    // Simplified healthcheck invocation for Docker HEALTHCHECK
    try
    {
        using var client = new HttpClient();
        var response = await client.GetAsync("http://localhost:8080/health/live");
        if (response.IsSuccessStatusCode)
        {
            return 0; // Success
        }
    }
    catch
    {
        // Ignore exception, return error code
    }
    return 1; // Error
}

if (args.Contains("--migrate") || args.Contains("--seed"))
{
    if (args.Contains("--migrate"))
    {
        using var scope = app.Services.CreateScope();
        var migrators = scope.ServiceProvider.GetServices<IModuleMigrator>();
        foreach (var migrator in migrators)
        {
            await migrator.MigrateAsync(CancellationToken.None);
        }
    }

    if (args.Contains("--seed"))
    {
        using var scope = app.Services.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IModuleSeeder>();
        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(CancellationToken.None);
        }
    }

    return 0;
}

app.UseBuildingBlocks();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Name == "live"
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            errors = report.Entries.Select(e => new { key = e.Key, value = e.Value.Status.ToString(), desc = e.Value.Description, ex = e.Value.Exception?.Message })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapInventoryEndpoints();
app.MapOrderingEndpoints();
app.MapPaymentEndpoints();
app.MapNotificationEndpoints();

app.Run();
return 0;

public partial class Program { }
