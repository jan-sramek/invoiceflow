using FluentValidation;
using FluentValidation.Results;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Application.Behaviors;

internal sealed class ValidationCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(command, validators, cancellationToken);
        return await inner.HandleAsync(command, cancellationToken);
    }
}

internal sealed class ValidationQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(query, validators, cancellationToken);
        return await inner.HandleAsync(query, cancellationToken);
    }
}

internal static class RequestValidation
{
    private static readonly IReadOnlyDictionary<string, object?> NoParams = new Dictionary<string, object?>();

    /// <summary>
    /// Validators set the error code with <c>WithErrorCode</c> and the params as a dictionary with
    /// <c>WithState</c>; the default FluentValidation messages never reach the client.
    /// </summary>
    /// <exception cref="RequestValidationException">At least one validator failed.</exception>
    public static async Task ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];
        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new RequestValidationException(
            [
                .. failures.Select(failure => new RequestFieldError(
                    failure.PropertyName,
                    failure.ErrorCode,
                    failure.CustomState as IReadOnlyDictionary<string, object?> ?? NoParams)),
            ]);
        }
    }
}
