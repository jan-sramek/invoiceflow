using FluentValidation;
using FluentValidation.Results;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Application.Invoices.UpdateFields;

internal sealed class UpdateInvoiceFieldsValidator : AbstractValidator<UpdateInvoiceFieldsCommand>
{
    public UpdateInvoiceFieldsValidator()
    {
        RuleFor(command => command.Fields).Custom((fields, context) =>
        {
            fields.ToFields(out IReadOnlyList<RequestFieldError> errors);

            foreach (RequestFieldError error in errors)
            {
                context.AddFailure(new ValidationFailure(error.Property, error.Code)
                {
                    ErrorCode = error.Code,
                    CustomState = error.Params,
                });
            }
        });
    }
}
