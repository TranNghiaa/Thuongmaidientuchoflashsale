using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShopFlow.Modules.Inventory.Infrastructure;

internal class ReservationSweeper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationSweeper> _logger;
    private readonly TimeProvider _timeProvider;

    public ReservationSweeper(IServiceProvider serviceProvider, ILogger<ReservationSweeper> logger, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationSweeper started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing ReservationSweeper.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var expiredReservations = await db.Reservations
            .Include(r => r.Items)
            .Where(r => r.Status == "Pending" && r.ExpiresAt < now)
            .ToListAsync(stoppingToken);

        if (!expiredReservations.Any()) return;

        foreach (var reservation in expiredReservations)
        {
            var skus = reservation.Items.Select(x => x.SkuId).ToList();
            var inventoryItems = await db.InventoryItems
                .Where(x => skus.Contains(x.SkuId))
                .ToListAsync(stoppingToken);

            foreach (var item in reservation.Items)
            {
                var invItem = inventoryItems.FirstOrDefault(x => x.SkuId == item.SkuId);
                if (invItem != null)
                {
                    invItem.ReservedQuantity -= item.Quantity;
                    invItem.AvailableQuantity += item.Quantity;
                }
            }

            reservation.Status = "Expired";
            _logger.LogInformation($"Released expired reservation: {reservation.ReferenceId}");
        }

        try
        {
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency exception occurred while sweeping reservations. Will retry next tick.");
        }
    }
}
