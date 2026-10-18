using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Errors;

public sealed class IdempotencyInProgressException()
    : InvoiceFlowException(ApplicationErrors.Idempotency.InProgress, ErrorKind.Conflict);

public sealed class IdempotencyKeyReusedException()
    : InvoiceFlowException(ApplicationErrors.Idempotency.KeyReused, ErrorKind.BusinessRule);

public sealed class IdempotencyKeyRequiredException()
    : InvoiceFlowException(ApplicationErrors.Idempotency.KeyRequired, ErrorKind.Validation);
