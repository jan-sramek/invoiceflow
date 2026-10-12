using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class UserRequiredException()
    : InvoiceFlowException(ApplicationErrors.Auth.UserRequired, ErrorKind.Unauthenticated);
