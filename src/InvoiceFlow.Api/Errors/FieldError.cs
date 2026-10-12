using System.Diagnostics.CodeAnalysis;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// One item of a ProblemDetails <c>errors</c> list; <see cref="Pointer"/> is empty when no single field is at fault.
/// </summary>
[SuppressMessage("Naming", "CA1720", Justification = "RFC 9457 names the member pointer.")]
public sealed record FieldError(string Pointer, string Code, IReadOnlyDictionary<string, object?> Params);
