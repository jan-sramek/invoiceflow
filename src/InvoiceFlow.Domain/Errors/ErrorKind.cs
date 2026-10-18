namespace InvoiceFlow.Domain.Errors;

public enum ErrorKind
{
    Validation,
    Unauthenticated,
    BusinessRule,
    NotFound,
    Conflict,
    Forbidden,
    PreconditionFailed,
    PreconditionRequired,
    PayloadTooLarge,
    UnsupportedMediaType,
    Unavailable,
}
