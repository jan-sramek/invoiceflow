using System.Text;
using System.Text.Json;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// Converts property paths into RFC 6901 JSON Pointers with camelCase names, the form the frontend
/// uses to find the form control.
/// </summary>
internal static class JsonPointer
{
    /// <summary>
    /// Accepts JSON paths from System.Text.Json (<c>$.lines[0].amount</c>) and property paths
    /// (<c>Lines[0].Amount</c>). An empty path or <c>$</c> points at the whole document.
    /// </summary>
    public static string FromPath(string path)
    {
        var pointer = new StringBuilder();

        foreach (string segment in path.TrimStart('$').Split(['.', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
        {
            pointer.Append('/').Append(Escape(JsonNamingPolicy.CamelCase.ConvertName(segment)));
        }

        return pointer.ToString();
    }

    private static string Escape(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
