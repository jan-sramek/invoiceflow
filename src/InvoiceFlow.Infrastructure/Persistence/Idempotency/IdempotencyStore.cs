using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyStore(TenantDbContext dbContext, TimeProvider time) : IIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(15);

    public async Task<IdempotencyBeginResult> BeginAsync(
        string key, string userId, string requestHash, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        IdempotencyRecord? existing = await dbContext.IdempotencyKeys
            .SingleOrDefaultAsync(r => r.Key == key && r.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            IdempotencyBeginResult? outcome = OutcomeOf(existing, requestHash, now);
            if (outcome is not null)
            {
                return outcome;
            }

            dbContext.IdempotencyKeys.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        dbContext.IdempotencyKeys.Add(new IdempotencyRecord
        {
            Key = key,
            UserId = userId,
            RequestHash = requestHash,
            CreatedAt = now,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IdempotencyBeginResult.Started();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            IdempotencyRecord concurrent = await dbContext.IdempotencyKeys
                .SingleAsync(r => r.Key == key && r.UserId == userId, cancellationToken);
            return OutcomeOf(concurrent, requestHash, now)
                ?? new IdempotencyBeginResult.InProgress();
        }
    }

    public async Task CompleteAsync(string key, string userId, Guid invoiceId, CancellationToken cancellationToken)
    {
        IdempotencyRecord record = await dbContext.IdempotencyKeys
            .SingleAsync(r => r.Key == key && r.UserId == userId, cancellationToken);
        record.InvoiceId = invoiceId;
    }

    public async Task AbandonAsync(string key, string userId, CancellationToken cancellationToken)
    {
        IdempotencyRecord? record = await dbContext.IdempotencyKeys
            .SingleOrDefaultAsync(r => r.Key == key && r.UserId == userId, cancellationToken);
        if (record is null || record.InvoiceId is not null)
        {
            return;
        }

        dbContext.IdempotencyKeys.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// <see langword="null"/> means the row is stale and may be replaced.
    /// </summary>
    private static IdempotencyBeginResult? OutcomeOf(IdempotencyRecord existing, string requestHash, DateTimeOffset now)
    {
        if (existing.CreatedAt < now - Retention)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new IdempotencyBeginResult.KeyReused();
        }

        if (existing.InvoiceId is { } invoiceId)
        {
            return new IdempotencyBeginResult.Completed(invoiceId);
        }

        return existing.CreatedAt < now - InProgressTimeout
            ? null
            : new IdempotencyBeginResult.InProgress();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
