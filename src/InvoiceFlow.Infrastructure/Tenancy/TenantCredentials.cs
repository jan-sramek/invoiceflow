using System.Text.Json;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// A tenant database login, read from a mounted Secret with one JSON file per tenant.
/// </summary>
internal sealed record TenantCredentials(string Username, string Password)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <exception cref="TenantUnavailableException">The file is missing, unreadable or incomplete.</exception>
    public static async Task<TenantCredentials> ReadAsync(
        string directory,
        string key,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(directory, key);

        try
        {
            await using FileStream file = File.OpenRead(path);
            TenantCredentials? credentials =
                await JsonSerializer.DeserializeAsync<TenantCredentials>(file, JsonOptions, cancellationToken);

            return credentials is { Username.Length: > 0, Password.Length: > 0 }
                ? credentials
                : throw new TenantUnavailableException();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new TenantUnavailableException(exception);
        }
    }

    public NpgsqlConnectionStringBuilder ConnectTo(
        TenantDatabase database,
        string? rootCertificatePath = null,
        int connectTimeoutSeconds = 15) =>
        ApplyTls(
            new NpgsqlConnectionStringBuilder
            {
                Host = database.Host,
                Port = database.Port,
                Database = database.DatabaseName,
                Username = Username,
                Password = Password,
                SslMode = database.SslMode,
                Timeout = connectTimeoutSeconds,
            }.WithoutGssEncryption(),
            database.SslMode,
            rootCertificatePath);

    private static NpgsqlConnectionStringBuilder ApplyTls(
        NpgsqlConnectionStringBuilder builder,
        SslMode sslMode,
        string? rootCertificatePath)
    {
        if (rootCertificatePath is { Length: > 0 }
            && sslMode is SslMode.VerifyCA or SslMode.VerifyFull)
        {
            builder.RootCertificate = rootCertificatePath;
        }

        return builder;
    }
}
