using System.Security.Cryptography;
using System.Text;

namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// The PDF stored for an invoice. Keys are generated; the original file name is metadata only.
/// </summary>
public sealed class InvoiceDocument
{
    public const int MaxFileNameLength = 255;
    public const string PdfContentType = "application/pdf";
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    // Used by EF Core when loading.
    private InvoiceDocument()
    {
        StorageKey = null!;
        OriginalFileName = null!;
        ContentType = null!;
        Sha256 = null!;
    }

    private InvoiceDocument(
        Guid id,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256)
    {
        Id = id;
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    public Guid Id { get; }

    public string StorageKey { get; }

    public string OriginalFileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    /// <summary>Lower-case hex SHA-256 of the file bytes.</summary>
    public string Sha256 { get; }

    public static InvoiceDocument Create(
        Guid id,
        string storageKey,
        string? originalFileName,
        long sizeBytes,
        string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        string hash = NormalizeSha256(sha256);
        string fileName = SanitizeFileName(originalFileName);

        return new InvoiceDocument(id, storageKey, fileName, PdfContentType, sizeBytes, hash);
    }

    public static string SanitizeFileName(string? originalFileName)
    {
        string raw = originalFileName?.Trim() ?? string.Empty;
        int separator = Math.Max(raw.LastIndexOf('/'), raw.LastIndexOf('\\'));
        string name = separator >= 0 ? raw[(separator + 1)..] : raw;
        if (name.Length == 0)
        {
            return "invoice.pdf";
        }

        var builder = new StringBuilder(Math.Min(name.Length, MaxFileNameLength));
        foreach (char c in name)
        {
            if (builder.Length >= MaxFileNameLength)
            {
                break;
            }

            builder.Append(c is < ' ' or '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' ? '_' : c);
        }

        string sanitised = builder.ToString().Trim('.', ' ');
        return sanitised.Length == 0 ? "invoice.pdf" : sanitised;
    }

    public static string NormalizeSha256(string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        string trimmed = sha256.Trim().ToLowerInvariant();
        if (trimmed.Length != SHA256.HashSizeInBytes * 2 || !trimmed.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("SHA-256 must be 64 hex characters.", nameof(sha256));
        }

        return trimmed;
    }
}
