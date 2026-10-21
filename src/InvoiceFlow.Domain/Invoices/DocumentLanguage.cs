using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// Normalises extractor-reported document languages to BCP 47 / ISO 639 tags.
/// Any language is accepted; there is no fixed allow-list.
/// </summary>
public static partial class DocumentLanguage
{
    public const int MaxLength = 32;

    /// <summary>
    /// Returns a language tag (e.g. <c>en</c>, <c>cs</c>, <c>zh-Hans</c>), or <c>null</c> when unknown/unusable.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            trimmed = trimmed[..MaxLength].Trim();
        }

        if (TagPattern().IsMatch(trimmed))
        {
            return CanonicalTag(trimmed);
        }

        return FromCultureName(trimmed);
    }

    private static string CanonicalTag(string tag)
    {
        try
        {
            // CultureInfo accepts BCP 47 tags and returns a canonical Name (e.g. zh-Hans).
            string name = CultureInfo.GetCultureInfo(tag).Name;
            return string.IsNullOrEmpty(name) || name == CultureInfo.InvariantCulture.Name
                ? tag.ToLowerInvariant()
                : name;
        }
        catch (CultureNotFoundException)
        {
            return tag.ToLowerInvariant();
        }
    }

    private static string? FromCultureName(string name)
    {
        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (culture.TwoLetterISOLanguageName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || culture.EnglishName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || culture.NativeName.Equals(name, StringComparison.OrdinalIgnoreCase)
                || culture.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                string iso = culture.TwoLetterISOLanguageName;
                return iso is { Length: > 0 } && iso != "iv" ? iso : culture.Name;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
