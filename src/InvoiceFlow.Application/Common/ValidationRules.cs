using FluentValidation;
using FluentValidation.Results;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Application.Common;

/// <summary>
/// Rules that report the API's error codes and params. Those that repeat a domain rule let the client get every
/// failing field with a pointer instead of the domain's first error. <c>field</c> is the name the client sees.
/// </summary>
internal static class ValidationRules
{
    public static void RequiredText<T>(this IRuleBuilderInitial<T, string?> rule, string field, int maxLength) =>
        rule.Custom((value, context) =>
        {
            int length = value?.Trim().Length ?? 0;

            if (length == 0)
            {
                Fail(context, DomainErrors.Validation.Required, field);
            }
            else if (length > maxLength)
            {
                Fail(context, DomainErrors.Validation.MaxLength, field, ("max", maxLength));
            }
        });

    public static void MaxLength<T>(this IRuleBuilderInitial<T, string?> rule, string field, int maxLength) =>
        rule.Custom((value, context) =>
        {
            if (value?.Trim().Length > maxLength)
            {
                Fail(context, DomainErrors.Validation.MaxLength, field, ("max", maxLength));
            }
        });

    public static void Required<T, TValue>(this IRuleBuilderInitial<T, TValue?> rule, string field)
        where TValue : struct =>
        rule.Custom((value, context) =>
        {
            if (value is null)
            {
                Fail(context, DomainErrors.Validation.Required, field);
            }
        });

    public static void RequiredObject<T, TValue>(this IRuleBuilderInitial<T, TValue?> rule, string field)
        where TValue : class =>
        rule.Custom((value, context) =>
        {
            if (value is null)
            {
                Fail(context, DomainErrors.Validation.Required, field);
            }
        });

    public static void InRange<T>(this IRuleBuilderInitial<T, int> rule, string field, int min, int max) =>
        rule.Custom((value, context) =>
        {
            if (value < min || value > max)
            {
                Fail(context, ApplicationErrors.Validation.OutOfRange, field, ("min", min), ("max", max));
            }
        });

    /// <summary>
    /// Reported on the end of the range, so <paramref name="field"/> names the end and <paramref name="fromField"/>
    /// the start.
    /// </summary>
    public static void NotBefore<T, TValue>(
        this IRuleBuilderInitial<T, TValue?> rule, Func<T, TValue?> from, string field, string fromField)
        where TValue : struct, IComparable<TValue> =>
        rule.Custom((to, context) =>
        {
            if (to is { } end && from(context.InstanceToValidate) is { } start && end.CompareTo(start) < 0)
            {
                Fail(context, ApplicationErrors.Validation.RangeReversed, field, ("from", fromField));
            }
        });

    public static void OneOf<T>(
        this IRuleBuilderInitial<T, string?> rule,
        Func<string, bool> isAllowed,
        IReadOnlyList<string> allowed,
        string field) =>
        rule.Custom((value, context) =>
        {
            if (value is not null && !isAllowed(value))
            {
                Fail(context, ApplicationErrors.Validation.NotAllowed, field, ("allowed", allowed));
            }
        });

    /// <summary>
    /// Runs the domain's factory for the value and reports its error, if any; a missing value is not checked.
    /// </summary>
    public static void MeetsDomainRules<T>(this IRuleBuilderInitial<T, string?> rule, Func<string, object> create) =>
        rule.Custom((value, context) =>
        {
            if (value is null)
            {
                return;
            }

            try
            {
                create(value);
            }
            catch (DomainValidationException exception)
            {
                AddFailure(context, exception.Code, exception.Params);
            }
        });

    private static void Fail<T>(
        ValidationContext<T> context, string code, string field, params (string Key, object? Value)[] parameters)
    {
        var all = new Dictionary<string, object?> { ["field"] = field };
        foreach ((string key, object? value) in parameters)
        {
            all[key] = value;
        }

        AddFailure(context, code, all);
    }

    private static void AddFailure<T>(
        ValidationContext<T> context, string code, IReadOnlyDictionary<string, object?> parameters) =>
        context.AddFailure(new ValidationFailure(context.PropertyPath, code)
        {
            ErrorCode = code,
            CustomState = parameters,
        });
}
