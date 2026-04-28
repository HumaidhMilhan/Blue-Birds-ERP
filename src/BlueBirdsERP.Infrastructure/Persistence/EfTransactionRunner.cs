using BlueBirdsERP.Application.POS;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class EfTransactionRunner(PoultryProDbContext dbContext) : ITransactionRunner
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
