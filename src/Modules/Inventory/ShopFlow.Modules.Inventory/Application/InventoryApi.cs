using Microsoft.EntityFrameworkCore;
using ShopFlow.Modules.Inventory.Contracts;
using ShopFlow.Modules.Inventory.Infrastructure;
using ShopFlow.Modules.Inventory.Domain;

namespace ShopFlow.Modules.Inventory.Application;

internal class InventoryApi : IInventoryApi
{
    private readonly InventoryDbContext _db;
    private readonly TimeProvider _timeProvider;

    public InventoryApi(InventoryDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<bool> ReserveAsync(string referenceId, IEnumerable<ReserveItemDto> items, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Reservations.FirstOrDefaultAsync(x => x.ReferenceId == referenceId, cancellationToken);
        if (existing != null) return false;

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            ReferenceId = referenceId,
            Status = "Pending",
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15)
        };

        var skus = items.Select(x => x.SkuId).ToList();
        var inventoryItems = await _db.InventoryItems
            .Where(x => skus.Contains(x.SkuId))
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var invItem = inventoryItems.FirstOrDefault(x => x.SkuId == item.SkuId);
            if (invItem == null || invItem.AvailableQuantity < item.Quantity)
            {
                return false;
            }

            invItem.AvailableQuantity -= item.Quantity;
            invItem.ReservedQuantity += item.Quantity;

            reservation.Items.Add(new ReservationItem
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                SkuId = item.SkuId,
                Quantity = item.Quantity
            });
        }

        _db.Reservations.Add(reservation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> CommitAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.ReferenceId == referenceId && r.Status == "Pending", cancellationToken);

        if (reservation == null) return false;

        var skus = reservation.Items.Select(x => x.SkuId).ToList();
        var inventoryItems = await _db.InventoryItems
            .Where(x => skus.Contains(x.SkuId))
            .ToListAsync(cancellationToken);

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
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> ReleaseAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.ReferenceId == referenceId && r.Status == "Pending", cancellationToken);

        if (reservation == null) return false;

        var skus = reservation.Items.Select(x => x.SkuId).ToList();
        var inventoryItems = await _db.InventoryItems
            .Where(x => skus.Contains(x.SkuId))
            .ToListAsync(cancellationToken);

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
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
