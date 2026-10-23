using System.Text.RegularExpressions;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Deterministic labels from PDF text used to correct small-model mistakes (wrong subtotal vs total, currency symbols).
/// </summary>
internal static partial class DocumentFieldHints
{
    public static InvoiceFields Enrich(InvoiceFields fields, string documentText)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return fields;
        }

        decimal? subtotal = BestAmount(SubtotalLabel(), documentText);
        decimal? tax = BestAmount(TaxLabel(), documentText);
        decimal? total = BestAmount(TotalLabel(), documentText) ?? BestAmount(TotalDueLabel(), documentText);
        string? currency = DetectCurrency(documentText);

        return fields with
        {
            Subtotal = subtotal ?? fields.Subtotal,
            TaxAmount = tax ?? fields.TaxAmount,
            TotalAmount = total ?? fields.TotalAmount,
            Currency = currency is null
                ? fields.Currency
                : fields.Currency ?? TryCurrency(currency),
        };
    }

    private static CurrencyCode? TryCurrency(string code)
    {
        try
        {
            return CurrencyCode.Create(code);
        }
        catch (DomainValidationException)
        {
            return null;
        }
    }

    private static string? DetectCurrency(string text)
    {
        if (text.Contains('$', StringComparison.Ordinal))
        {
            return "USD";
        }

        if (text.Contains('€', StringComparison.Ordinal))
        {
            return "EUR";
        }

        if (text.Contains('£', StringComparison.Ordinal))
        {
            return "GBP";
        }

        if (text.Contains("Kč", StringComparison.OrdinalIgnoreCase)
            || text.Contains("CZK", StringComparison.OrdinalIgnoreCase))
        {
            return "CZK";
        }

        return null;
    }

    /// <summary>
    /// Prefers currency-marked / decimal amounts (summary totals) over bare integers from table headers
    /// like <c>Sub Total1.00</c> (quantity) before <c>Sub Total$85.00</c>.
    /// </summary>
    private static decimal? BestAmount(Regex label, string text)
    {
        MatchCollection matches = label.Matches(text);
        if (matches.Count == 0)
        {
            return null;
        }

        decimal? best = null;
        int bestScore = int.MinValue;
        foreach (Match match in matches)
        {
            string raw = match.Groups[1].Value;
            if (!InvoiceExtractionJson.TryParseAmountString(raw, out decimal amount))
            {
                continue;
            }

            amount = decimal.Round(amount, Money.MaxDecimals, MidpointRounding.AwayFromZero);
            int score = 0;
            if (raw.Contains('$') || raw.Contains('€') || raw.Contains('£'))
            {
                score += 100;
            }

            if (raw.Contains('.') || raw.Contains(','))
            {
                score += 10;
            }

            // Prefer later matches (invoice summaries sit under the line items).
            score += match.Index / 1000;
            if (score >= bestScore)
            {
                bestScore = score;
                best = amount;
            }
        }

        return best;
    }

    // Compact PDF text often has no space: "Sub Total$85.00Tax$8.50Total$93.50"
    // Also: "AdjustSub Total1.00Web Design...Sub Total$85.00" — BestAmount prefers $85.00.
    [GeneratedRegex(
        @"Sub\s*Total\s*[:\-]?\s*([€$£]?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubtotalLabel();

    [GeneratedRegex(
        @"(?<!Sub\s)Tax(?!\s*ID)\s*[:\-]?\s*([€$£]?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TaxLabel();

    [GeneratedRegex(
        @"(?<!Sub\s)Total(?!\s*Due)\s*[:\-]?\s*([€$£]?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalLabel();

    [GeneratedRegex(
        @"Total\s*Due\s*[:\-]?\s*([€$£]?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalDueLabel();
}
