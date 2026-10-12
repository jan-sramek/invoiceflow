using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Common;

internal static class Text
{
    public static string Require(string? value, string field, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.Required,
                new Dictionary<string, object?> { ["field"] = field });
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.MaxLength,
                new Dictionary<string, object?> { ["field"] = field, ["max"] = maxLength });
        }

        return trimmed;
    }
}
