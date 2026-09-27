using System.Data;
using IplStore.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IplStore.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> on EF Core: execution strategy (retry) -> transaction -> work ->
/// SaveChanges -> commit. See the interface docs for the contract.
/// </summary>
/// <remarks>
/// Commit ambiguity: if the connection drops DURING commit, we cannot know whether it
/// committed. The retry then re-runs the use case - which is why checkout is idempotent
/// (the replay finds the committed order instead of creating a second one).
/// </remarks>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly StoreDbContext _db;
    private readonly ILogger<EfUnitOfWork> _logger;

    public EfUnitOfWork(StoreDbContext db, ILogger<EfUnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        var strategy = _db.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(
            async ct =>
            {
                attempt++;
                if (attempt > 1)
                {
                    _logger.LogWarning("Retrying database transaction, attempt {Attempt}, after a transient failure", attempt);
                }

                // A retry must start from a clean slate: forget entities tracked by the failed attempt.
                _db.ChangeTracker.Clear();

                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                var result = await work(ct);
                await SaveChangesTranslatingConflictsAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            },
            cancellationToken);
    }

    private async Task SaveChangesTranslatingConflictsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The data was changed by another request. Please reload and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new ConcurrencyConflictException(
                $"A concurrent request already created this record (constraint '{pg.ConstraintName}').", ex);
        }
    }
}
