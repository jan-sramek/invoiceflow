namespace InvoiceFlow.Application.Abstractions.Storage;

/// <summary>
/// Keeps uploaded documents outside the database. Every member throws
/// <see cref="Errors.StorageUnavailableException"/> when the storage cannot be reached.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Stores <paramref name="content"/> from its current position to the end. An existing document under the
    /// same key is replaced.
    /// </summary>
    Task StoreAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <exception cref="KeyNotFoundException">Nothing is stored under the key.</exception>
    Task<Stream> OpenAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Deleting a key that holds nothing is not an error.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
