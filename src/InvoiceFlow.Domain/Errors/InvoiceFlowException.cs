using System.Globalization;

namespace InvoiceFlow.Domain.Errors;

/// <summary>
/// Base class for expected failures. <see cref="Params"/> must never contain user input or document content.
/// </summary>
public abstract class InvoiceFlowException : Exception
{
    protected InvoiceFlowException(string code, ErrorKind kind, IReadOnlyDictionary<string, object?>? parameters = null)
        : base(BuildMessage(code, parameters))
    {
        Code = code;
        Kind = kind;
        Params = parameters ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public ErrorKind Kind { get; }

    public IReadOnlyDictionary<string, object?> Params { get; }

    private static string BuildMessage(string code, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return code;
        }

        IEnumerable<string> pairs = parameters.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.Key}={p.Value}"));
        return $"{code} ({string.Join(", ", pairs)})";
    }
}
