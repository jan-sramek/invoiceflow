using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Domain.Invoices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace InvoiceFlow.Api.Invoices;

[ApiController]
[Route("api/v1/invoices")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InvoicesController : ControllerBase
{
    /// <summary>
    /// One invoice with its version in <c>ETag</c>; send it back as <c>If-Match</c> when changing the invoice.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<InvoiceDetailDto> Get(
        Guid id,
        [FromServices] IQueryHandler<GetInvoiceQuery, Versioned<InvoiceDetailDto>> handler,
        CancellationToken cancellationToken)
    {
        Versioned<InvoiceDetailDto> invoice = await handler.HandleAsync(new GetInvoiceQuery(new InvoiceId(id)), cancellationToken);
        Response.GetTypedHeaders().ETag = new EntityTagHeaderValue($"\"{invoice.Version}\"");
        return invoice.Value;
    }
}
