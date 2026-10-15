using InvoiceFlow.Api.Http;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Approve;
using InvoiceFlow.Application.Invoices.Cancel;
using InvoiceFlow.Application.Invoices.EnterManually;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Application.Invoices.MarkPaid;
using InvoiceFlow.Application.Invoices.Reject;
using InvoiceFlow.Application.Invoices.Reopen;
using InvoiceFlow.Application.Invoices.Submit;
using InvoiceFlow.Application.Invoices.UpdateFields;
using InvoiceFlow.Domain.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Invoices;

/// <summary>
/// Every write needs the invoice's <c>ETag</c> in <c>If-Match</c> and answers with the changed invoice and
/// its new <c>ETag</c>.
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class InvoicesController(IQueryHandler<GetInvoiceQuery, Versioned<InvoiceDetailDto>> getInvoice)
    : ControllerBase
{
    /// <summary>
    /// Pages through the tenant's invoices, newest upload first unless <c>sort</c> says otherwise.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedList<InvoiceSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedList<InvoiceSummaryDto>> List(
        [FromQuery] InvoiceListRequest request,
        [FromServices] IQueryHandler<ListInvoicesQuery, PagedList<InvoiceSummaryDto>> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request.ToQuery(), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    public Task<InvoiceDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        DetailAsync(new InvoiceId(id), cancellationToken);

    /// <summary>
    /// Replaces all editable fields; a missing or blank value clears the field.
    /// </summary>
    [HttpPut("{id:guid}/fields")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> UpdateFields(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        InvoiceFieldsInput fields,
        [FromServices] ICommandHandler<UpdateInvoiceFieldsCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new UpdateInvoiceFieldsCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), fields),
            cancellationToken);

    [HttpPost("{id:guid}/enter-manually")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> EnterManually(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<EnterInvoiceManuallyCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new EnterInvoiceManuallyCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);

    /// <summary>
    /// Sends the invoice for approval; a 422 lists the fields that still block it.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> Submit(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<SubmitInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new SubmitInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);

    /// <summary>
    /// Approves the invoice; a 403 means the caller submitted it.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> Approve(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<ApproveInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new ApproveInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);

    /// <summary>
    /// Rejects the invoice with a reason; a 403 means the caller submitted it.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> Reject(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ReasonRequest request,
        [FromServices] ICommandHandler<RejectInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new RejectInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), request.Reason),
            cancellationToken);

    /// <summary>
    /// Returns a rejected invoice to review so its fields can be corrected.
    /// </summary>
    [HttpPost("{id:guid}/reopen")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> Reopen(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<ReopenInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new ReopenInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);

    /// <summary>
    /// Records that an approved invoice was paid in full.
    /// </summary>
    [HttpPost("{id:guid}/mark-paid")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> MarkPaid(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        PaymentRequest request,
        [FromServices] ICommandHandler<MarkInvoicePaidCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new MarkInvoicePaidCommand(
                new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), request.PaidOn, request.PaymentReference),
            cancellationToken);

    /// <summary>
    /// Cancels an invoice that is not approved yet; it stays in the history and is never deleted.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> Cancel(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ReasonRequest request,
        [FromServices] ICommandHandler<CancelInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new CancelInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), request.Reason),
            cancellationToken);

    private async Task<InvoiceDetailDto> ChangeAsync<TCommand>(
        ICommandHandler<TCommand, InvoiceId> handler, TCommand command, CancellationToken cancellationToken)
        where TCommand : IInvoiceCommand
    {
        InvoiceId id = await handler.HandleAsync(command, cancellationToken);
        return await DetailAsync(id, cancellationToken);
    }

    private async Task<InvoiceDetailDto> DetailAsync(InvoiceId id, CancellationToken cancellationToken)
    {
        Versioned<InvoiceDetailDto> invoice = await getInvoice.HandleAsync(new GetInvoiceQuery(id), cancellationToken);
        Response.GetTypedHeaders().ETag = EntityTags.From(invoice.Version);
        return invoice.Value;
    }
}
