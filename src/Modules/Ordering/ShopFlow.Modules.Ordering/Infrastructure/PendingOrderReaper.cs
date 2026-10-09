using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopFlow.Modules.Inventory.Contracts;
using ShopFlow.Modules.Ordering.Infrastructure;

namespace ShopFlow.Modules.Ordering.Infrastructure;

internal class PendingOrderReaper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingOrderReaper> _logger;
    private readonly TimeProvider _timeProvider;

    public PendingOrderReaper(IServiceProvider serviceProvider, ILogger<PendingOrderReaper> logger, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingOrderReaper started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing PendingOrderReaper.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var inventoryApi = scope.ServiceProvider.GetRequiredService<IInventoryApi>();
        
        var threshold = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-15);

        var expiredOrders = await db.Orders
            .Where(o => o.Status == "PendingPayment" && o.CreatedAt < threshold)
            .ToListAsync(stoppingToken);

        if (!expiredOrders.Any()) return;

        foreach (var order in expiredOrders)
        {
            order.Status = "Cancelled";
            _logger.LogInformation($"Cancelled expired pending order: {order.Id}");
            
            await inventoryApi.ReleaseAsync(order.ReferenceId, stoppingToken);
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
