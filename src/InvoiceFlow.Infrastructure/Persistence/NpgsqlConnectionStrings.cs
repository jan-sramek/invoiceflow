using Npgsql;

namespace InvoiceFlow.Infrastructure.Persistence;

internal static class NpgsqlConnectionStrings
{
    /// <summary>
    /// The chiseled runtime images have no Kerberos library. With GSS encryption left at its default,
    /// Npgsql looks for it on every new physical connection and prints an error.
    /// </summary>
    public static NpgsqlConnectionStringBuilder WithoutGssEncryption(this NpgsqlConnectionStringBuilder builder)
    {
        builder.GssEncryptionMode = GssEncryptionMode.Disable;
        return builder;
    }
}
