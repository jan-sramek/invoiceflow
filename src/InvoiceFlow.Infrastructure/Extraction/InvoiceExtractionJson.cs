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
            JsonValueKind.String when decimal.TryParse(
                property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) => parsed,
            _ => null,
        };
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
