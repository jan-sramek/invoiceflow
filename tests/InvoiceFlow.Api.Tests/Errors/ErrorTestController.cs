using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Tests.Errors;

/// <summary>Endpoints that fail in every way the error contract covers.</summary>
[ApiController]
[Route("test/errors")]
public sealed class ErrorTestController : ControllerBase
{
    public const string UnexpectedMessage = "unexpected-failure-message";

    [HttpGet("validation")]
    public IActionResult Validation() => throw new DomainValidationException(
        DomainErrors.Validation.MaxLength,
        new Dictionary<string, object?> { ["field"] = "supplierName", ["max"] = 200 });

    [HttpGet("business-rule")]
    public IActionResult BusinessRule() => throw new BusinessRuleException(DomainErrors.Invoice.PaymentDateInFuture);

    [HttpGet("conflict")]
    public IActionResult InvalidTransition() => throw new InvalidTransitionException(InvoiceStatus.Paid, InvoiceAction.Cancel);

    [HttpGet("forbidden")]
    public IActionResult OwnInvoice() => throw new ForbiddenException(DomainErrors.Approval.OwnInvoice);

    [HttpGet("stale")]
    public IActionResult Stale() => throw new ConcurrencyException();

    [HttpGet("unavailable")]
    public IActionResult Unavailable() =>
        throw new TenantUnavailableException(new InvalidOperationException(UnexpectedMessage));

    [HttpGet("not-ready")]
    public IActionResult NotReady() => throw new InvoiceNotReadyException(
    [
        new FieldIssue(InvoiceField.TotalAmount, DomainErrors.Validation.TotalMismatch),
        new FieldIssue(InvoiceField.DueDate, DomainErrors.Validation.Required),
    ]);

    [HttpGet("invalid-request")]
    public IActionResult InvalidRequest() => throw new RequestValidationException(
    [
        new RequestFieldError("Lines[0].Amount", DomainErrors.Validation.AmountPrecision, new Dictionary<string, object?> { ["maxDecimals"] = 2 }),
        new RequestFieldError("SupplierName", DomainErrors.Validation.Required, new Dictionary<string, object?>()),
    ]);

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException(UnexpectedMessage);

    [HttpPost("body")]
    public IActionResult Body(TestRequest request) => Ok(request);
}

public sealed record TestRequest(string Name, IReadOnlyList<TestLine> Lines);

public sealed record TestLine(decimal Amount);
