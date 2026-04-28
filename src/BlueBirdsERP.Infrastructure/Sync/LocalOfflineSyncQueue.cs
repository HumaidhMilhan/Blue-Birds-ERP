using BlueBirdsERP.Application.Abstractions;

namespace BlueBirdsERP.Infrastructure.Sync;

public sealed class LocalOfflineSyncQueue : IOfflineSyncQueue
{
    public Task EnqueueAsync(OfflineSyncEnvelope envelope, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}

