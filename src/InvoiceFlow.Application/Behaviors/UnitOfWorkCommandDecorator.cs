using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;

namespace InvoiceFlow.Application.Behaviors;

internal sealed class UnitOfWorkCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IUnitOfWork unitOfWork) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        TResult result = await inner.HandleAsync(command, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return result;
    }
}
