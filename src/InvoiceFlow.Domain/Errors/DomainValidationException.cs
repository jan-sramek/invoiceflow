namespace InvoiceFlow.Domain.Errors;

public sealed class DomainValidationException(string code, IReadOnlyDictionary<string, object?>? parameters = null)
    : InvoiceFlowException(code, ErrorKind.Validation, parameters);
