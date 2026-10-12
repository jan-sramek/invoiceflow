namespace InvoiceFlow.Domain.Errors;

public enum ErrorKind
{
    Validation,
    BusinessRule,
    NotFound,
    Conflict,
    Forbidden,
    PreconditionFailed,
    PreconditionRequired,
    Unavailable,
}
