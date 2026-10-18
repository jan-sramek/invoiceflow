using InvoiceFlow.Api.Http;
using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.AcknowledgeDuplicate;
using InvoiceFlow.Application.Invoices.Approve;
using InvoiceFlow.Application.Invoices.Cancel;
using InvoiceFlow.Application.Invoices.DownloadDocument;
using InvoiceFlow.Application.Invoices.EnterManually;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.GetInvoiceAudit;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Application.Invoices.MarkPaid;
using InvoiceFlow.Application.Invoices.Reject;
using InvoiceFlow.Application.Invoices.Reopen;
using InvoiceFlow.Application.Invoices.Submit;
using InvoiceFlow.Application.Invoices.UpdateFields;
using InvoiceFlow.Application.Invoices.Upload;
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
public sealed class InvoicesController(
    IQueryHandler<GetInvoiceQuery, Versioned<InvoiceDetailDto>> getInvoice,
    IDocumentStorage documents) : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

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

    /// <summary>
    /// Accepts one PDF and creates an invoice. Send a unique <c>Idempotency-Key</c> so a retry cannot create a
    /// second invoice. Batch uploads are several parallel calls of this endpoint.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    [RequestSizeLimit(InvoiceDocument.MaxSizeBytes + 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = InvoiceDocument.MaxSizeBytes + 1024)]
    public async Task<InvoiceDetailDto> Upload(
        IFormFile? file,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        [FromServices] ICommandHandler<UploadInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new UploadNotPdfException();
        }

        if (file.Length > InvoiceDocument.MaxSizeBytes)
        {
            throw new UploadTooLargeException();
        }

        await using Stream content = file.OpenReadStream();
        InvoiceId id = await handler.HandleAsync(
            new UploadInvoiceCommand(content, file.FileName, idempotencyKey ?? string.Empty),
            cancellationToken);

        Response.StatusCode = StatusCodes.Status202Accepted;
        return await DetailAsync(id, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    public Task<InvoiceDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        DetailAsync(new InvoiceId(id), cancellationToken);

    /// <summary>
    /// Downloads the original PDF. The download is audited before the bytes are sent.
    /// </summary>
    [HttpGet("{id:guid}/document")]
    [Authorize]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Download(
        Guid id,
        [FromServices] ICommandHandler<DownloadInvoiceDocumentCommand, InvoiceDocumentAccess> handler,
        CancellationToken cancellationToken)
    {
        InvoiceDocumentAccess access = await handler.HandleAsync(
            new DownloadInvoiceDocumentCommand(new InvoiceId(id)), cancellationToken);
        Stream content = await documents.OpenReadAsync(access.StorageKey, cancellationToken);
        return File(content, access.ContentType, access.FileName);
    }

    /// <summary>
    /// The invoice's audit timeline, oldest first.
    /// </summary>
    [HttpGet("{id:guid}/audit")]
    [ProducesResponseType<PagedList<InvoiceAuditEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedList<InvoiceAuditEntryDto>> Audit(
        Guid id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] IQueryHandler<GetInvoiceAuditQuery, PagedList<InvoiceAuditEntryDto>> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(
            new GetInvoiceAuditQuery(
                new InvoiceId(id),
                page ?? 1,
                pageSize ?? GetInvoiceAuditQuery.DefaultPageSize),
            cancellationToken);

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
    /// Confirms that a suspected duplicate is not a duplicate of the linked invoice.
    /// </summary>
    [HttpPost("{id:guid}/acknowledge-not-duplicate")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public Task<InvoiceDetailDto> AcknowledgeNotDuplicate(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ReasonRequest request,
        [FromServices] ICommandHandler<AcknowledgeNotDuplicateCommand, InvoiceId> handler,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            handler,
            new AcknowledgeNotDuplicateCommand(
                new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), request.Reason),
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
