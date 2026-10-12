using System.Globalization;

namespace InvoiceFlow.Infrastructure.Persistence;

/// <summary>
/// The opaque version string the application sees for PostgreSQL's <c>xmin</c>.
/// </summary>
internal static class RowVersion
{
    public static string Format(uint xmin) => xmin.ToString(CultureInfo.InvariantCulture);
}
