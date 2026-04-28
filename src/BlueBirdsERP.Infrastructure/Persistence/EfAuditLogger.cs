using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class EfAuditLogger(PoultryProDbContext dbContext) : IAuditLogger
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            UserId = entry.UserId,
            Role = entry.Role,
            Action = entry.Action,
            Module = entry.Module,
            TargetEntity = entry.TargetEntity,
            TargetId = entry.TargetId,
            BeforeValueJson = entry.BeforeValueJson,
            AfterValueJson = entry.AfterValueJson,
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
