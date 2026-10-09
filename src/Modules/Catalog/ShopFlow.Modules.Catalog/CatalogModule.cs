using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.Modules.Catalog.Domain;
using ShopFlow.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShopFlow.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IModuleMigrator, CatalogMigrator>();
        services.AddScoped<IModuleSeeder, CatalogSeeder>();
        services.AddScoped<ShopFlow.Modules.Catalog.Contracts.ICatalogApi, ShopFlow.Modules.Catalog.Application.CatalogApi>();

        var minioUrl = configuration["Minio:Endpoint"] ?? configuration["Minio:ServiceURL"];
        var minioAccessKey = configuration["Minio:AccessKey"];
        var minioSecretKey = configuration["Minio:SecretKey"];

        if (!string.IsNullOrWhiteSpace(minioUrl))
        {
            services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
            {
                var config = new Amazon.S3.AmazonS3Config
                {
                    ServiceURL = minioUrl,
                    ForcePathStyle = true
                };
                var credentials = new Amazon.Runtime.BasicAWSCredentials(minioAccessKey, minioSecretKey);
                return new Amazon.S3.AmazonS3Client(credentials, config);
            });
        }

        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("").WithTags("Catalog");

        group.MapPost("/products", [Authorize(Policy = "AdminOnly")] async (CreateProductRequest req, CatalogDbContext db, TimeProvider timeProvider) =>
        {
            if (req.BasePrice <= 0)
                return Results.BadRequest(new { code = "INVALID_PRICE", message = "Base price must be greater than 0." });

            var product = new Product
            {
                Id = Guid.NewGuid(),
                SkuId = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(), // Simple SKU generator
                Name = req.Name,
                Description = req.Description,
                BasePrice = req.BasePrice,
                IsActive = true,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/products/{product.Id}", product);
        });

        group.MapPut("/products/{id:guid}", [Authorize(Policy = "AdminOnly")] async (Guid id, UpdateProductRequest req, CatalogDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product == null) return Results.NotFound();

            if (req.BasePrice <= 0)
                return Results.BadRequest(new { code = "INVALID_PRICE", message = "Base price must be greater than 0." });

            product.Name = req.Name;
            product.Description = req.Description;
            product.BasePrice = req.BasePrice;
            product.IsActive = req.IsActive;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/products", async (
            [FromQuery] int page, 
            [FromQuery] int pageSize, 
            [FromQuery] string? q, 
            HttpContext ctx, 
            CatalogDbContext db, 
            TimeProvider timeProvider) =>
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = db.Products.AsQueryable();

            if (!ctx.User.IsInRole("Admin"))
            {
                query = query.Where(p => p.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLowerInvariant();
                query = query.Where(p => p.Name.ToLower().Contains(qLower));
            }

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.CreatedAt)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            var skuIds = items.Select(x => x.SkuId).ToList();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            
            // Get active flash sales
            var flashSales = await db.FlashSales
                .Where(f => skuIds.Contains(f.SkuId) && f.StartsAt <= now && f.EndsAt > now)
                .ToListAsync();

            var resultItems = items.Select(p =>
            {
                var sale = flashSales.FirstOrDefault(f => f.SkuId == p.SkuId);
                var effectivePrice = sale != null ? sale.SalePrice : p.BasePrice;
                return new ProductDto
                {
                    Id = p.Id,
                    SkuId = p.SkuId,
                    Name = p.Name,
                    Description = p.Description,
                    BasePrice = p.BasePrice,
                    EffectivePrice = effectivePrice,
                    IsFlashSale = sale != null,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                };
            });

            return Results.Ok(new
            {
                items = resultItems,
                page,
                pageSize,
                total
            });
        });

        group.MapPost("/admin/flash-sales", [Authorize(Policy = "AdminOnly")] async (CreateFlashSaleRequest req, CatalogDbContext db) =>
        {
            if (req.StartsAt >= req.EndsAt || req.EndsAt <= DateTime.UtcNow)
                return Results.BadRequest(new { code = "INVALID_TIME", message = "Invalid time range." });

            var product = await db.Products.FirstOrDefaultAsync(p => p.SkuId == req.SkuId);
            if (product == null)
                return Results.NotFound(new { code = "SKU_NOT_FOUND", message = "SKU not found." });

            if (req.SalePrice <= 0 || req.SalePrice >= product.BasePrice)
                return Results.BadRequest(new { code = "INVALID_SALE_PRICE", message = "Sale price must be > 0 and < base price." });

            var overlap = await db.FlashSales.AnyAsync(f => 
                f.SkuId == req.SkuId && 
                f.StartsAt < req.EndsAt && 
                f.EndsAt > req.StartsAt);

            if (overlap)
                return Results.Conflict(new { code = "FLASH_SALE_OVERLAP" });

            var flashSale = new FlashSale
            {
                Id = Guid.NewGuid(),
                SkuId = req.SkuId,
                SalePrice = req.SalePrice,
                StartsAt = req.StartsAt,
                EndsAt = req.EndsAt
            };

            db.FlashSales.Add(flashSale);
            await db.SaveChangesAsync();

            return Results.Created($"/admin/flash-sales/{flashSale.Id}", flashSale);
        });

        group.MapPut("/products/{id:guid}/image", [Authorize(Policy = "AdminOnly")] async (Guid id, UploadImageRequest req, Amazon.S3.IAmazonS3 s3Client, CatalogDbContext db, TimeProvider timeProvider) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product == null) return Results.NotFound();

            if (!req.Key.StartsWith($"products/{id}/"))
                return Results.BadRequest(new { code = "INVALID_KEY", message = $"Key must start with products/{id}/" });

            var ext = System.IO.Path.GetExtension(req.Key).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
                return Results.BadRequest(new { code = "INVALID_EXTENSION", message = "Only jpg, jpeg, png, webp are allowed." });

            var putRequest = new Amazon.S3.Model.GetPreSignedUrlRequest
            {
                BucketName = "product-images",
                Key = req.Key,
                Verb = Amazon.S3.HttpVerb.PUT,
                Expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10)
            };
            
            var getRequest = new Amazon.S3.Model.GetPreSignedUrlRequest
            {
                BucketName = "product-images",
                Key = req.Key,
                Verb = Amazon.S3.HttpVerb.GET,
                Expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10)
            };

            var putUrl = s3Client.GetPreSignedURL(putRequest);
            var getUrl = s3Client.GetPreSignedURL(getRequest);

            return Results.Ok(new { putUrl, getUrl });
        });

        return endpoints;
    }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
}

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
}

public class CreateFlashSaleRequest
{
    public string SkuId { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string SkuId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public bool IsFlashSale { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UploadImageRequest
{
    public string Key { get; set; } = string.Empty;
}
