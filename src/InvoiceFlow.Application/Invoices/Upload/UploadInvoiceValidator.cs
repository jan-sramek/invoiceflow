using FluentValidation;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Application.Invoices.Upload;

internal sealed class UploadInvoiceValidator : AbstractValidator<UploadInvoiceCommand>
{
    public const int MaxIdempotencyKeyLength = 128;

    public UploadInvoiceValidator() =>
        RuleFor(c => c.IdempotencyKey).Custom((key, context) =>
        {
            string trimmed = key?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(context.PropertyPath, ApplicationErrors.Idempotency.KeyRequired)
                {
                    ErrorCode = ApplicationErrors.Idempotency.KeyRequired,
                    CustomState = new Dictionary<string, object?>(),
                });
            }
            else if (trimmed.Length > MaxIdempotencyKeyLength)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(context.PropertyPath, Domain.Errors.DomainErrors.Validation.MaxLength)
                {
                    ErrorCode = Domain.Errors.DomainErrors.Validation.MaxLength,
                    CustomState = new Dictionary<string, object?>
                    {
                        ["field"] = "idempotencyKey",
                        ["max"] = MaxIdempotencyKeyLength,
                    },
                });
            }
        });
}
