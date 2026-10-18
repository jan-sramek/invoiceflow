namespace InvoiceFlow.Domain.Invoices;

/// <summary>
/// The PDF an invoice was uploaded as. The file name is the client's, cleaned up: it is shown and offered on
/// download, but the file is only ever found by its storage key.
/// </summary>
public sealed class InvoiceDocument
{
    public const int MaxFileNameLength = 255;
    public const string DefaultFileName = "invoice.pdf";
    public const int Sha256Length = 64;

    // Used by EF Core when loading.
    private InvoiceDocument()
    {
        StorageKey = null!;
        FileName = null!;
        Sha256 = null!;
    }

    private InvoiceDocument(Guid id, string storageKey, string fileName, long sizeBytes, string sha256)
    {
        Id = id;
        StorageKey = storageKey;
        FileName = fileName;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    public Guid Id { get; }

    public string StorageKey { get; }

    public string FileName { get; }

    public long SizeBytes { get; }

    /// <summary>
    /// Lowercase hex; identical files have the same value.
    /// </summary>
    public string Sha256 { get; }

    /// <exception cref="ArgumentException">The key is blank, the size not positive or the hash not lowercase hex.</exception>
    public static InvoiceDocument Create(Guid id, string storageKey, string? fileName, long sizeBytes, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        if (sha256.Length != Sha256Length || !sha256.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'))
        {
            throw new ArgumentException("Expected a lowercase hex SHA-256.", nameof(sha256));
        }

        return new InvoiceDocument(id, storageKey, CleanFileName(fileName), sizeBytes, sha256);
    }

    // Keeps only the last path segment, without control characters or surrounding spaces.
    private static string CleanFileName(string? fileName)
    {
        string lastSegment = (fileName ?? string.Empty).Replace('\\', '/').Split('/')[^1];
        string name = new string([.. lastSegment.Where(c => !char.IsControl(c))]).Trim();

        if (name.Length > MaxFileNameLength)
        {
            int length = char.IsHighSurrogate(name[MaxFileNameLength - 1]) ? MaxFileNameLength - 1 : MaxFileNameLength;
            name = name[..length].TrimEnd();
        }

        return name.Length == 0 ? DefaultFileName : name;
    }
}
