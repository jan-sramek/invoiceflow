using System.Globalization;
using System.Text;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Deterministic per-field validation and grounding against PdfPig text (ADR-0011 / PLAN §3.8).
/// </summary>
internal static class ExtractedFieldAssessor
{
    public static InvoiceFieldStatuses Assess(InvoiceFields fields, string documentText, DateOnly today)
    {
        string haystack = Normalize(documentText);
        var invalid = fields.FindIssues(today).Select(i => i.Field).ToHashSet();

        return new InvoiceFieldStatuses
        {
            SupplierName = StatusOf(fields.SupplierName?.Value, InvoiceField.SupplierName, invalid, haystack),
            InvoiceNumber = StatusOf(fields.InvoiceNumber?.Value, InvoiceField.InvoiceNumber, invalid, haystack),
            InvoiceDate = StatusOf(Format(fields.InvoiceDate), InvoiceField.InvoiceDate, invalid, haystack, date: fields.InvoiceDate),
            DueDate = StatusOf(Format(fields.DueDate), InvoiceField.DueDate, invalid, haystack, date: fields.DueDate),
            Currency = StatusOf(fields.Currency?.Value, InvoiceField.Currency, invalid, haystack),
            Subtotal = StatusOf(Format(fields.Subtotal), InvoiceField.Subtotal, invalid, haystack, amount: fields.Subtotal),
            TaxAmount = StatusOf(Format(fields.TaxAmount), InvoiceField.TaxAmount, invalid, haystack, amount: fields.TaxAmount),
            TotalAmount = StatusOf(Format(fields.TotalAmount), InvoiceField.TotalAmount, invalid, haystack, amount: fields.TotalAmount),
            PurchaseOrderReference = StatusOf(
                fields.PurchaseOrderReference?.Value,
                InvoiceField.PurchaseOrderReference,
                invalid,
                haystack),
        };
    }

    private static FieldStatus StatusOf(
        string? display,
        InvoiceField field,
        HashSet<InvoiceField> invalid,
        string haystack,
        DateOnly? date = null,
        decimal? amount = null)
    {
        if (string.IsNullOrWhiteSpace(display) && date is null && amount is null)
        {
            return FieldStatus.Missing;
        }

        if (invalid.Contains(field))
        {
            return FieldStatus.Invalid;
        }

        return IsGrounded(haystack, display, date, amount) ? FieldStatus.Valid : FieldStatus.Unverified;
    }

    private static bool IsGrounded(string haystack, string? display, DateOnly? date, decimal? amount)
    {
        if (!string.IsNullOrWhiteSpace(display))
        {
            string needle = Normalize(display);
            if (haystack.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }

            // ISO currency codes often appear only as symbols ($ / € / £ / Kč).
            if (CurrencySymbolGrounds(needle, haystack))
            {
                return true;
            }
        }

        if (date is { } d && DateForms(d).Any(form => haystack.Contains(Normalize(form), StringComparison.Ordinal)))
        {
            return true;
        }

        if (amount is { } value)
        {
            string[] forms =
            [
                value.ToString("0.00", CultureInfo.InvariantCulture),
                value.ToString("0.##", CultureInfo.InvariantCulture),
                value.ToString("#,##0.00", CultureInfo.InvariantCulture),
                value.ToString("# ##0,00", CultureInfo.GetCultureInfo("cs-CZ")).Replace('\u00a0', ' '),
                value.ToString("#.##0,00", CultureInfo.GetCultureInfo("de-DE")),
            ];
            if (forms.Any(form => haystack.Contains(Normalize(form), StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CurrencySymbolGrounds(string currencyCode, string haystack) =>
        currencyCode switch
        {
            "usd" => haystack.Contains('$') || haystack.Contains("usd", StringComparison.Ordinal),
            "eur" => haystack.Contains('€') || haystack.Contains("eur", StringComparison.Ordinal),
            "gbp" => haystack.Contains('£') || haystack.Contains("gbp", StringComparison.Ordinal),
            "czk" => haystack.Contains("kč", StringComparison.Ordinal)
                || haystack.Contains("czk", StringComparison.Ordinal)
                || haystack.Contains("kc", StringComparison.Ordinal),
            _ => false,
        };

    private static IEnumerable<string> DateForms(DateOnly d)
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        var cs = CultureInfo.GetCultureInfo("cs-CZ");
        var de = CultureInfo.GetCultureInfo("de-DE");
        yield return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        yield return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        yield return d.ToString("d.M.yyyy", CultureInfo.InvariantCulture);
        yield return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        yield return d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        yield return d.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
        // "January 25, 2016" / "25 January 2016" (common English invoice PDFs)
        yield return d.ToString("MMMM d, yyyy", en);
        yield return d.ToString("MMMM dd, yyyy", en);
        yield return d.ToString("d MMMM yyyy", en);
        yield return d.ToString("dd MMMM yyyy", en);
        yield return d.ToString("d. MMMM yyyy", cs);
        yield return d.ToString("d. MMMM yyyy", de);
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length == 0 || builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString().Trim();
    }

    private static string? Format(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Format(decimal? amount) =>
        amount?.ToString("0.00", CultureInfo.InvariantCulture);

}
