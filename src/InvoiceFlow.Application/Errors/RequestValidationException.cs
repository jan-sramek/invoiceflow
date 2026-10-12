using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

/// <summary>
/// A command or query failed its validator. Every failing field is reported, not only the first.
/// </summary>
public sealed class RequestValidationException(IReadOnlyList<RequestFieldError> errors)
    : InvoiceFlowException(ApplicationErrors.Validation.Failed, ErrorKind.Validation)
{
    public IReadOnlyList<RequestFieldError> Errors { get; } = errors;
}

/// <summary>
/// One failing field; <see cref="Property"/> is its path in the request, such as <c>Lines[0].Amount</c>.
/// </summary>
public sealed record RequestFieldError(string Property, string Code, IReadOnlyDictionary<string, object?> Params);
