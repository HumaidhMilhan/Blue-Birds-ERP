using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Sync;

public sealed class LocalOfflineSyncQueue(PoultryProDbContext dbContext) : IOfflineSyncQueue
{
    public async Task EnqueueAsync(OfflineSyncEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await dbContext.OfflineSyncQueueItems.AddAsync(new OfflineSyncQueueItem
        {
            EntityName = envelope.EntityName,
            EntityId = envelope.EntityId,
            Operation = envelope.Operation,
            PayloadJson = envelope.PayloadJson,
            QueuedAt = envelope.QueuedAt,
            Status = OfflineSyncStatus.Pending
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        var pendingItems = await dbContext.OfflineSyncQueueItems
            .Where(item => item.Status == OfflineSyncStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var item in pendingItems)
        {
            item.Status = OfflineSyncStatus.Processing;
            item.LastAttemptedAt = DateTimeOffset.UtcNow;
            item.RetryCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pendingItems.Count;
    }
}
