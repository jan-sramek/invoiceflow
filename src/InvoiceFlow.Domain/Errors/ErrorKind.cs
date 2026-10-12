namespace InvoiceFlow.Domain.Errors;

public enum ErrorKind
{
    Validation,
    BusinessRule,
    Conflict,
    Forbidden,
    PreconditionFailed,
    Unavailable,
}
