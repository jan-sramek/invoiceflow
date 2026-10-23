using System.Globalization;
using System.Text.Json;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Parses model JSON into <see cref="InvoiceFields"/>, ignoring invalid individual values.
/// </summary>
internal static class InvoiceExtractionJson
{
    public static InvoiceFields Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return InvoiceFields.Empty;
        }

        string json = ExtractJsonObject(text);
        if (json.Length == 0)
        {
            return InvoiceFields.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvoiceFields.Empty;
            }

            return new InvoiceFields
            {
                SupplierName = TryCreate(root, "supplierName", SupplierName.Create),
                InvoiceNumber = TryCreate(root, "invoiceNumber", InvoiceNumber.Create),
                InvoiceDate = TryDate(root, "invoiceDate"),
                DueDate = TryDate(root, "dueDate"),
                Currency = TryCreate(root, "currency", CurrencyCode.Create),
                Subtotal = TryAmount(root, "subtotal"),
                TaxAmount = TryAmount(root, "taxAmount"),
                TotalAmount = TryAmount(root, "totalAmount"),
                PurchaseOrderReference = TryCreate(root, "purchaseOrderReference", PurchaseOrderReference.Create),
            };
        }
        catch (JsonException)
        {
            return InvoiceFields.Empty;
        }
        catch (DomainValidationException)
        {
            return InvoiceFields.Empty;
        }
    }

    internal static string ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return "";
        }

        return text[start..(end + 1)];
    }

    internal static string? TryLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string json = ExtractJsonObject(text);
        if (json.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryGetString(document.RootElement, "documentLanguage", out string? language) ? language : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T? TryCreate<T>(JsonElement root, string name, Func<string?, T> factory)
        where T : class
    {
        if (!TryGetString(root, name, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return factory(value);
        }
        catch (DomainValidationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static DateOnly? TryDate(JsonElement root, string name)
    {
        if (!TryGetString(root, name, out string? text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }

    private static decimal? TryAmount(JsonElement root, string name)
    {
        decimal? raw = TryDecimal(root, name);
        if (raw is null)
        {
            return null;
        }

        decimal rounded = decimal.Round(raw.Value, Money.MaxDecimals, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) > Money.MaxAbsoluteAmount)
        {
            return null;
        }

        return rounded;
    }

    private static decimal? TryDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out decimal number) => number,
            JsonValueKind.String when TryParseAmountString(property.GetString(), out decimal parsed) => parsed,
            // Models sometimes emit "$93.50" without quotes → invalid JSON; when coerced via ToString on other kinds, skip.
            _ => null,
        };
    }

    /// <summary>
    /// Accepts plain numbers plus common invoice spellings: <c>$93.50</c>, <c>93,50</c>, <c>1.234,56</c>, <c>1,234.56</c>.
    /// </summary>
    internal static bool TryParseAmountString(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Span<char> buffer = stackalloc char[text.Length];
        int length = 0;
        foreach (char c in text.Trim())
        {
            if (c is '-' && length == 0)
            {
                buffer[length++] = c;
                continue;
            }

            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                buffer[length++] = c;
            }
        }

        if (length == 0 || (length == 1 && buffer[0] == '-'))
        {
            return false;
        }

        string cleaned = new(buffer[..length]);
        int lastDot = cleaned.LastIndexOf('.');
        int lastComma = cleaned.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            // Rightmost separator is the decimal; the other is thousands.
            char decimalSep = lastDot > lastComma ? '.' : ',';
            char thousandsSep = decimalSep == '.' ? ',' : '.';
            cleaned = cleaned.Replace(thousandsSep.ToString(), "", StringComparison.Ordinal)
                .Replace(decimalSep, '.');
        }
        else if (lastComma >= 0)
        {
            int digitsAfter = cleaned.Length - lastComma - 1;
            // "1,234" (thousands) vs "93,50" / "8,5" (decimal).
            bool thousands = digitsAfter == 3 && lastComma > 0;
            if (thousands)
            {
                for (int i = 0; i < lastComma; i++)
                {
                    char c = cleaned[i];
                    if (c == '-' && i == 0)
                    {
                        continue;
                    }

                    if (!char.IsAsciiDigit(c))
                    {
                        thousands = false;
                        break;
                    }
                }
            }

            cleaned = thousands
                ? cleaned.Replace(",", "", StringComparison.Ordinal)
                : cleaned.Replace(',', '.');
        }

        return decimal.TryParse(
            cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return true;
    }
}
