namespace InvoiceFlow.Application.Abstractions.Persistence;

/// <summary>
/// Remembers the outcome of an idempotent request for 24 hours so a retry does not create a second resource.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Begins work for <paramref name="key"/> and <paramref name="userId"/>, or returns a prior outcome.
    /// </summary>
    Task<IdempotencyBeginResult> BeginAsync(
        string key, string userId, string requestHash, CancellationToken cancellationToken);

    Task CompleteAsync(string key, string userId, Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops an in-progress claim so a failed attempt can be retried with the same key.
    /// </summary>
    Task AbandonAsync(string key, string userId, CancellationToken cancellationToken);
}

public abstract record IdempotencyBeginResult
{
    public sealed record Started : IdempotencyBeginResult;

    public sealed record Completed(Guid InvoiceId) : IdempotencyBeginResult;

    public sealed record InProgress : IdempotencyBeginResult;

    public sealed record KeyReused : IdempotencyBeginResult;
}
