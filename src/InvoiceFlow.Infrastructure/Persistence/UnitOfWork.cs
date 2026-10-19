using System.Diagnostics;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Audit;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class UnitOfWork(TenantDbContext dbContext) : IUnitOfWork
{
    private readonly List<Func<CancellationToken, Task>> _afterCommit = [];

    public void AfterCommit(Func<CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _afterCommit.Add(callback);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        AggregateRoot[] aggregates =
        [
            .. dbContext.ChangeTracker.Entries<AggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0),
        ];
        string? correlationId = Activity.Current?.TraceId.ToHexString();

        foreach (IDomainEvent domainEvent in aggregates.SelectMany(aggregate => aggregate.DomainEvents))
        {
            dbContext.AuditEntries.Add(AuditEntryFactory.Create(domainEvent, dbContext.TenantId, correlationId));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }

        foreach (AggregateRoot aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        Func<CancellationToken, Task>[] callbacks = [.. _afterCommit];
        _afterCommit.Clear();
        foreach (Func<CancellationToken, Task> callback in callbacks)
        {
            await callback(cancellationToken);
        }
    }
}
