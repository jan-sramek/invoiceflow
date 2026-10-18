using FluentValidation;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.AcknowledgeDuplicate;

internal sealed class AcknowledgeNotDuplicateValidator : AbstractValidator<AcknowledgeNotDuplicateCommand>
{
    public AcknowledgeNotDuplicateValidator() =>
        RuleFor(command => command.Reason).RequiredText("reason", Invoice.MaxReasonLength);
}
