using System.Collections.Concurrent;
using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Testing.Storage;

/// <summary>
/// Keeps documents in memory. While <see cref="Unavailable"/> is set, every call fails the way the real storage
/// does during an outage.
/// </summary>
public sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, StoredDocument> _documents = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public IReadOnlyDictionary<string, StoredDocument> Documents => _documents;

    public async Task StoreAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _documents[key] = new StoredDocument(buffer.ToArray(), contentType);
    }

    public Task<Stream> OpenAsync(string key, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return _documents.TryGetValue(key, out StoredDocument? document)
            ? Task.FromResult<Stream>(new MemoryStream(document.Content, writable: false))
            : Task.FromException<Stream>(new KeyNotFoundException($"No document is stored under '{key}'."));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        _documents.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new StorageUnavailableException();
        }
    }
}

public sealed record StoredDocument(byte[] Content, string ContentType);
