using InvoiceFlow.Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Behaviors;

internal sealed class LoggingCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILoggerFactory loggerFactory) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private static readonly string UseCase = UseCaseExecution.NameOf(typeof(TCommand));

    private readonly ILogger _logger = loggerFactory.CreateLogger(UseCaseExecution.LoggerCategory);

    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        UseCaseExecution.RunAsync(UseCase, _logger, () => inner.HandleAsync(command, cancellationToken));
}

internal sealed class LoggingQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILoggerFactory loggerFactory) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly string UseCase = UseCaseExecution.NameOf(typeof(TQuery));

    private readonly ILogger _logger = loggerFactory.CreateLogger(UseCaseExecution.LoggerCategory);

    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        UseCaseExecution.RunAsync(UseCase, _logger, () => inner.HandleAsync(query, cancellationToken));
}
