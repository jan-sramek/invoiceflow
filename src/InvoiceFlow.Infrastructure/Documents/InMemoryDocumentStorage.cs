using System.Collections.Concurrent;
using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Infrastructure.Documents;

/// <summary>
/// Process-local storage for tests. Not registered in production.
/// </summary>
public sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public async Task PutAsync(
        string key, Stream content, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[key] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        _objects.TryGetValue(key, out byte[]? bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new StorageUnavailableException();

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
