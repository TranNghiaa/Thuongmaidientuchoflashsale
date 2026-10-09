using Microsoft.EntityFrameworkCore;

namespace ShopFlow.BuildingBlocks.Messaging;

public interface IHasOutbox
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
