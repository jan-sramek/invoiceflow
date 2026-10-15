using FluentValidation;
using FluentValidation.Results;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Common;

/// <summary>
/// Request rules that repeat a domain rule, so the client gets every failing field with a pointer instead of the
/// domain's first error. <c>field</c> is the name the domain reports, so both send the same params.
/// </summary>
internal static class ValidationRules
{
    public static void RequiredText<T>(this IRuleBuilderInitial<T, string?> rule, string field, int maxLength) =>
        rule.Custom((value, context) =>
        {
            int length = value?.Trim().Length ?? 0;

            if (length == 0)
            {
                Fail(context, DomainErrors.Validation.Required, new() { ["field"] = field });
            }
            else if (length > maxLength)
            {
                Fail(context, DomainErrors.Validation.MaxLength, new() { ["field"] = field, ["max"] = maxLength });
            }
        });

    public static void Required<T, TValue>(this IRuleBuilderInitial<T, TValue?> rule, string field)
        where TValue : struct =>
        rule.Custom((value, context) =>
        {
            if (value is null)
            {
                Fail(context, DomainErrors.Validation.Required, new() { ["field"] = field });
            }
        });

    private static void Fail<T>(ValidationContext<T> context, string code, Dictionary<string, object?> parameters) =>
        context.AddFailure(new ValidationFailure(context.PropertyPath, code)
        {
            ErrorCode = code,
            CustomState = parameters,
        });
}
