namespace InvoiceFlow.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes together with an audit entry for every domain event raised, in one transaction.
    /// </summary>
    /// <exception cref="Errors.ConcurrencyException">Another request changed the data first.</exception>
    Task CommitAsync(CancellationToken cancellationToken);
}
