namespace InvoiceFlow.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes together with an audit entry for every domain event raised, in one transaction.
    /// Then runs any callbacks registered with <see cref="AfterCommit"/>.
    /// </summary>
    /// <exception cref="Errors.ConcurrencyException">Another request changed the data first.</exception>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers work that runs only after a successful <see cref="CommitAsync"/> in this scope.
    /// Callbacks are cleared after they run (or if the scope ends without committing).
    /// </summary>
    void AfterCommit(Func<CancellationToken, Task> callback);
}
