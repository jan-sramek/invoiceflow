using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.UpdateFields;

internal sealed class UpdateInvoiceFieldsHandler(
    IInvoiceRepository invoices,
    ICurrentUser user,
    TimeProvider time) : ICommandHandler<UpdateInvoiceFieldsCommand, InvoiceId>
{
    public async Task<InvoiceId> HandleAsync(UpdateInvoiceFieldsCommand command, CancellationToken cancellationToken)
    {
        InvoiceFields fields = command.Fields.ToFields(out IReadOnlyList<RequestFieldError> errors);
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        Invoice invoice = await invoices.GetAsync(command.Id, command.ExpectedVersion, cancellationToken)
            ?? throw new InvoiceNotFoundException();
        invoice.UpdateFields(user.Actor, time.GetUtcNow(), fields);

        return invoice.Id;
    }
}
