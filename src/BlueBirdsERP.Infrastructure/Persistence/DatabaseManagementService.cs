using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class DatabaseManagementService(
    PoultryProDbContext dbContext,
    InfrastructureOptions options,
    IAuditLogger auditLogger) : IDatabaseManagementService
{
    public async Task<DatabaseOperationResult> TestSqliteConnectionAsync(
        AdminOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        try
        {
            await using var connection = new SqliteConnection(options.Database.LocalPosConnectionString);
            await connection.OpenAsync(cancellationToken);
            await WriteAuditAsync(request, "SQLITE_CONNECTION_TEST", "true", cancellationToken);
            return new DatabaseOperationResult(true, "SQLite connection succeeded.");
        }
        catch (Exception ex)
        {
            await WriteAuditAsync(request, "SQLITE_CONNECTION_TEST", "false", cancellationToken);
            return new DatabaseOperationResult(false, ex.Message);
        }
    }

    public async Task<DatabaseOperationResult> TestPostgreSqlConnectionAsync(
        AdminOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        if (string.IsNullOrWhiteSpace(options.Database.CentralConnectionString))
        {
            return new DatabaseOperationResult(false, "PostgreSQL connection string is empty.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(options.Database.CentralConnectionString);
            await connection.OpenAsync(cancellationToken);
            await WriteAuditAsync(request, "POSTGRES_CONNECTION_TEST", "true", cancellationToken);
            return new DatabaseOperationResult(true, "PostgreSQL connection succeeded.");
        }
        catch (Exception ex)
        {
            await WriteAuditAsync(request, "POSTGRES_CONNECTION_TEST", "false", cancellationToken);
            return new DatabaseOperationResult(false, ex.Message);
        }
    }

    public async Task<DatabaseOperationResult> ApplyMigrationsAsync(
        AdminOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await WriteAuditAsync(request, "DATABASE_SCHEMA_APPLY", "true", cancellationToken);
        return new DatabaseOperationResult(true, "SQLite schema is ready.");
    }

    public async Task<DatabaseBackupResult> BackupSqliteDatabaseAsync(
        DatabaseBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var sourcePath = GetSqliteDatabasePath(options.Database.LocalPosConnectionString);
        if (string.IsNullOrWhiteSpace(sourcePath) || string.Equals(sourcePath, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseBackupResult(false, string.Empty, "SQLite database path is not backupable.");
        }

        var absoluteSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(absoluteSourcePath))
        {
            return new DatabaseBackupResult(false, string.Empty, "SQLite database file was not found.");
        }

        var destinationDirectory = Path.GetFullPath(request.DestinationDirectory ?? options.Database.BackupDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var backupPath = Path.Combine(destinationDirectory, $"bluebirds-backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.sqlite3");
        File.Copy(absoluteSourcePath, backupPath, overwrite: false);

        await WriteAuditAsync(new AdminOperationRequest(request.UserId, request.Role), "SQLITE_BACKUP_CREATE", "true", cancellationToken);
        return new DatabaseBackupResult(true, backupPath, "SQLite backup created.");
    }

    public async Task<OfflineSyncQueueStatusResult> GetSyncQueueStatusAsync(
        AdminOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        var items = await dbContext.OfflineSyncQueueItems.ToListAsync(cancellationToken);
        return new OfflineSyncQueueStatusResult(
            items.Count(item => item.Status == OfflineSyncStatus.Pending),
            items.Count(item => item.Status == OfflineSyncStatus.Processing),
            items.Count(item => item.Status == OfflineSyncStatus.Completed),
            items.Count(item => item.Status == OfflineSyncStatus.Failed));
    }

    private async Task WriteAuditAsync(
        AdminOperationRequest request,
        string action,
        string succeeded,
        CancellationToken cancellationToken)
    {
        await auditLogger.WriteAsync(new AuditEntry(
            request.UserId,
            request.Role,
            action,
            "DATABASE",
            nameof(PoultryProDbContext),
            Guid.Empty,
            null,
            $"{{\"succeeded\":{succeeded}}}"), cancellationToken);
    }

    private static string GetSqliteDatabasePath(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }

    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new InvalidOperationException("Only Admin users can manage database settings.");
        }
    }
}
