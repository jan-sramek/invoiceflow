namespace InvoiceFlow.Application.Abstractions.Documents;

/// <summary>
/// Stores invoice PDFs behind the S3 API. Keys are opaque and never derived from client file names.
/// </summary>
public interface IDocumentStorage
{
    Task PutAsync(string key, Stream content, string contentType, long sizeBytes, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
