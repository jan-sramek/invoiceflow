namespace InvoiceFlow.Domain.Errors;

public sealed class ForbiddenException(string code, IReadOnlyDictionary<string, object?>? parameters = null)
    : InvoiceFlowException(code, ErrorKind.Forbidden, parameters);
