namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// Deterministic assessment of an extracted (or edited) invoice field. Model confidence is ignored.
/// </summary>
public enum FieldStatus
{
    /// <summary>No value present.</summary>
    Missing,

    /// <summary>Present, passes rules, and grounded in the source text (or accepted by a human edit).</summary>
    Valid,

    /// <summary>Present but fails a deterministic business rule.</summary>
    Invalid,

    /// <summary>Present and rule-ok but not found in the source document text.</summary>
    Unverified,
}
