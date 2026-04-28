using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class AuditLogReader(PoultryProDbContext dbContext) : IAuditLogReader
{
    public async Task<IReadOnlyList<AuditLogEntryResult>> QueryAsync(
        AuditLogQuery request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("Only Admin users can read audit logs.");
        }

        var query = dbContext.AuditLogs.AsQueryable();

        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(log => log.Timestamp >= from);
        }

        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(log => log.Timestamp < to);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(log => log.UserId == request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            query = query.Where(log => log.Module == request.Module.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(log => log.Action == request.Action.Trim());
        }

        return await query
            .OrderByDescending(log => log.Timestamp)
            .Select(log => new AuditLogEntryResult(
                log.LogId,
                log.UserId,
                log.Role,
                log.Action,
                log.Module,
                log.TargetEntity,
                log.TargetId,
                log.Timestamp))
            .ToListAsync(cancellationToken);
    }
}
