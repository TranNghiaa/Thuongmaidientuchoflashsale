using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.Modules.Inventory.Domain;
using ShopFlow.Modules.Inventory.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShopFlow.Modules.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IModuleMigrator, InventoryMigrator>();
        services.AddScoped<ShopFlow.Modules.Inventory.Contracts.IInventoryApi, ShopFlow.Modules.Inventory.Application.InventoryApi>();
        services.AddHostedService<ReservationSweeper>();

        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("").WithTags("Inventory");

        group.MapPost("/admin/inventory", [Authorize(Policy = "AdminOnly")] async (AddInventoryRequest req, InventoryDbContext db, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.SkuId))
                return Results.BadRequest(new { code = "INVALID_SKU", message = "SkuId is required." });

            if (req.Quantity <= 0)
                return Results.BadRequest(new { code = "INVALID_QUANTITY", message = "Quantity must be > 0." });

            var item = await db.InventoryItems.FirstOrDefaultAsync(x => x.SkuId == req.SkuId);
            if (item == null)
            {
                item = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    SkuId = req.SkuId,
                    AvailableQuantity = req.Quantity,
                    ReservedQuantity = 0,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime
                };
                db.InventoryItems.Add(item);
            }
            else
            {
                item.AvailableQuantity += req.Quantity;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { item.SkuId, item.AvailableQuantity });
        });

        group.MapGet("/inventory/{skuId}", async (string skuId, InventoryDbContext db) =>
        {
            var item = await db.InventoryItems.FirstOrDefaultAsync(x => x.SkuId == skuId);
            if (item == null)
                return Results.NotFound();

            return Results.Ok(new { item.SkuId, item.AvailableQuantity });
        });

        group.MapPost("/inventory/reserve", async (ReserveRequest req, InventoryDbContext db, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.ReferenceId) || !req.Items.Any())
                return Results.BadRequest(new { code = "INVALID_REQUEST", message = "ReferenceId and Items are required." });

            var skus = req.Items.Select(x => x.SkuId).ToList();
            var inventoryItems = await db.InventoryItems.Where(x => skus.Contains(x.SkuId)).ToListAsync();

            if (inventoryItems.Count != skus.Count)
                return Results.BadRequest(new { code = "SKU_NOT_FOUND", message = "Some SKUs not found." });

            foreach (var reqItem in req.Items)
            {
                var invItem = inventoryItems.First(x => x.SkuId == reqItem.SkuId);
                if (invItem.AvailableQuantity < reqItem.Quantity)
                {
                    return Results.BadRequest(new { code = "OUT_OF_STOCK", message = $"Not enough stock for SKU {reqItem.SkuId}" });
                }
            }

            // Perform reservation
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                ReferenceId = req.ReferenceId,
                ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15),
                Status = "Pending"
            };

            foreach (var reqItem in req.Items)
            {
                var invItem = inventoryItems.First(x => x.SkuId == reqItem.SkuId);
                invItem.AvailableQuantity -= reqItem.Quantity;
                invItem.ReservedQuantity += reqItem.Quantity;
                
                reservation.Items.Add(new ReservationItem
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    SkuId = reqItem.SkuId,
                    Quantity = reqItem.Quantity
                });
            }

            db.Reservations.Add(reservation);
            
            try
            {
                await db.SaveChangesAsync();
                return Results.Ok(new { reservation.Id, reservation.ReferenceId, reservation.ExpiresAt });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { code = "CONCURRENCY_ERROR" });
            }
        });

        group.MapPost("/inventory/commit", async (CommitRequest req, InventoryDbContext db) =>
        {
            var reservation = await db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.ReferenceId == req.ReferenceId);
            
            if (reservation == null)
                return Results.NotFound(new { code = "RESERVATION_NOT_FOUND" });

            if (reservation.Status != "Pending")
                return Results.BadRequest(new { code = "INVALID_STATUS", message = $"Cannot commit. Current status: {reservation.Status}" });

            var skus = reservation.Items.Select(x => x.SkuId).ToList();
            var inventoryItems = await db.InventoryItems.Where(x => skus.Contains(x.SkuId)).ToListAsync();

            foreach (var item in reservation.Items)
            {
                var invItem = inventoryItems.FirstOrDefault(x => x.SkuId == item.SkuId);
                if (invItem != null)
                {
                    invItem.ReservedQuantity -= item.Quantity;
                }
            }

            reservation.Status = "Committed";
            
            try
            {
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { code = "CONCURRENCY_ERROR" });
            }
        });

        group.MapPost("/inventory/release", async (ReleaseRequest req, InventoryDbContext db) =>
        {
            var reservation = await db.Reservations.Include(r => r.Items).FirstOrDefaultAsync(r => r.ReferenceId == req.ReferenceId);
            
            if (reservation == null)
                return Results.NotFound(new { code = "RESERVATION_NOT_FOUND" });

            if (reservation.Status != "Pending")
                return Results.BadRequest(new { code = "INVALID_STATUS", message = $"Cannot release. Current status: {reservation.Status}" });

            var skus = reservation.Items.Select(x => x.SkuId).ToList();
            var inventoryItems = await db.InventoryItems.Where(x => skus.Contains(x.SkuId)).ToListAsync();

            foreach (var item in reservation.Items)
            {
                var invItem = inventoryItems.FirstOrDefault(x => x.SkuId == item.SkuId);
                if (invItem != null)
                {
                    invItem.ReservedQuantity -= item.Quantity;
                    invItem.AvailableQuantity += item.Quantity;
                }
            }

            reservation.Status = "Released";
            
            try
            {
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { code = "CONCURRENCY_ERROR" });
            }
        });

        return endpoints;
    }
}

public class ReserveRequest
{
    public string ReferenceId { get; set; } = string.Empty;
    public List<ReserveItem> Items { get; set; } = new();
}

public class ReserveItem
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CommitRequest
{
    public string ReferenceId { get; set; } = string.Empty;
}

public class ReleaseRequest
{
    public string ReferenceId { get; set; } = string.Empty;
}

public class AddInventoryRequest
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
