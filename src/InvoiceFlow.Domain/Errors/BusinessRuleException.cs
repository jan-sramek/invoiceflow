namespace InvoiceFlow.Domain.Errors;

public sealed class BusinessRuleException(string code, IReadOnlyDictionary<string, object?>? parameters = null)
    : InvoiceFlowException(code, ErrorKind.BusinessRule, parameters);
