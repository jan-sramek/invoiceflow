using InvoiceFlow.Api.Http;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.EnterManually;
using InvoiceFlow.Application.Invoices.GetInvoice;
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
    public async Task<InvoiceDetailDto> UpdateFields(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        InvoiceFieldsInput fields,
        [FromServices] ICommandHandler<UpdateInvoiceFieldsCommand, InvoiceId> handler,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = await handler.HandleAsync(
            new UpdateInvoiceFieldsCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch), fields),
            cancellationToken);
        return await DetailAsync(invoiceId, cancellationToken);
    }

    [HttpPost("{id:guid}/enter-manually")]
    [Authorize]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<InvoiceDetailDto> EnterManually(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<EnterInvoiceManuallyCommand, InvoiceId> handler,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = await handler.HandleAsync(
            new EnterInvoiceManuallyCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);
        return await DetailAsync(invoiceId, cancellationToken);
    }

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
    public async Task<InvoiceDetailDto> Submit(
        Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] ICommandHandler<SubmitInvoiceCommand, InvoiceId> handler,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = await handler.HandleAsync(
            new SubmitInvoiceCommand(new InvoiceId(id), EntityTags.RequiredVersion(ifMatch)),
            cancellationToken);
        return await DetailAsync(invoiceId, cancellationToken);
    }

    private async Task<InvoiceDetailDto> DetailAsync(InvoiceId id, CancellationToken cancellationToken)
    {
        Versioned<InvoiceDetailDto> invoice = await getInvoice.HandleAsync(new GetInvoiceQuery(id), cancellationToken);
        Response.GetTypedHeaders().ETag = EntityTags.From(invoice.Version);
        return invoice.Value;
    }
}
