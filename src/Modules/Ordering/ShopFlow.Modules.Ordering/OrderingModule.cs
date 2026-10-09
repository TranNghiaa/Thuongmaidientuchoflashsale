using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.BuildingBlocks.Messaging;
using ShopFlow.Modules.Ordering.Infrastructure;

namespace ShopFlow.Modules.Ordering;

public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<OrderingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IModuleMigrator, OrderingMigrator>();
        services.AddScoped<ShopFlow.Modules.Ordering.Application.OrderPricingService>();
        services.AddHostedService<ShopFlow.Modules.Ordering.Infrastructure.PendingOrderReaper>();
        services.AddOutboxPublisher<OrderingDbContext>();

        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("").WithTags("Ordering");

        group.MapPost("/orders", [Microsoft.AspNetCore.Authorization.Authorize] async (
            PlaceOrderRequest req, 
            ShopFlow.Modules.Ordering.Application.OrderPricingService pricingService,
            ShopFlow.Modules.Inventory.Contracts.IInventoryApi inventoryApi,
            ShopFlow.Modules.Payment.Contracts.IPaymentApi paymentApi,
            OrderingDbContext db, 
            HttpContext ctx,
            TimeProvider timeProvider) =>
        {
            if (req.Items == null || !req.Items.Any())
                return Results.BadRequest(new { code = "EMPTY_ORDER", message = "Order must contain at least one item." });

            // Step 1: Calculate Pricing
            var pricingItems = req.Items.Select(x => new ShopFlow.Modules.Ordering.Application.OrderPricingItem { SkuId = x.SkuId, Quantity = x.Quantity });
            var pricingResult = await pricingService.CalculatePricingAsync(pricingItems);

            var referenceId = Guid.NewGuid().ToString("N");

            // Step 2: Reserve Inventory
            var reserveItems = req.Items.Select(x => new ShopFlow.Modules.Inventory.Contracts.ReserveItemDto { SkuId = x.SkuId, Quantity = x.Quantity });
            var reserved = await inventoryApi.ReserveAsync(referenceId, reserveItems);
            
            if (!reserved)
            {
                return Results.BadRequest(new { code = "INVENTORY_SHORTAGE", message = "Not enough inventory." });
            }

            // Step 3: Create Order
            var userId = Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            
            var order = new ShopFlow.Modules.Ordering.Domain.Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReferenceId = referenceId,
                Status = "PendingPayment",
                TotalAmount = pricingResult.TotalAmount,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };

            foreach (var item in pricingResult.Items)
            {
                order.Items.Add(new ShopFlow.Modules.Ordering.Domain.OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    SkuId = item.SkuId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            // Step 4: Process Payment
            var paid = await paymentApi.ProcessPaymentAsync(referenceId, pricingResult.TotalAmount);
            if (paid)
            {
                await inventoryApi.CommitAsync(referenceId);
                order.Status = "Paid";

                db.OutboxMessages.Add(new ShopFlow.BuildingBlocks.Messaging.OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = typeof(ShopFlow.Modules.Ordering.Contracts.Events.OrderPaid).AssemblyQualifiedName ?? "",
                    Content = System.Text.Json.JsonSerializer.Serialize(new ShopFlow.Modules.Ordering.Contracts.Events.OrderPaid(order.Id, order.UserId, order.TotalAmount)),
                    OccurredOn = timeProvider.GetUtcNow().UtcDateTime
                });

                await db.SaveChangesAsync();
            }
            else
            {
                await inventoryApi.ReleaseAsync(referenceId);
                order.Status = "Failed";
                await db.SaveChangesAsync();
            }

            return Results.Ok(new { order.Id, order.Status, order.TotalAmount });
        });

        group.MapPost("/orders-async", [Microsoft.AspNetCore.Authorization.Authorize] async (
            PlaceOrderRequest req,
            HttpContext ctx,
            MassTransit.IBus bus) =>
        {
            var userIdStr = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            await bus.Publish(new ShopFlow.Modules.Ordering.Contracts.Events.OrderPlacedAsync(userId, req.Items.Select(x => new ShopFlow.Modules.Ordering.Contracts.Events.OrderPlacedAsyncItem(x.SkuId, x.Quantity)).ToList()), ctx.RequestAborted);
            
            return Results.Accepted();
        });

        return endpoints;
    }
}

internal class PlaceOrderRequest
{
    public List<PlaceOrderItemRequest> Items { get; set; } = new();
}

internal class PlaceOrderItemRequest
{
    public string SkuId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
