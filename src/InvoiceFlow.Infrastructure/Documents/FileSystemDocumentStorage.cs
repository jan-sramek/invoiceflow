using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Errors;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Documents;

/// <summary>
/// Shared on-disk store for local multi-process development (API + Worker).
/// </summary>
internal sealed class FileSystemDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public FileSystemDocumentStorage(IOptions<FileSystemDocumentStorageOptions> options)
    {
        FileSystemDocumentStorageOptions settings = options.Value;
        _root = Path.GetFullPath(settings.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task PutAsync(
        string key, Stream content, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);
        _ = contentType;
        _ = sizeBytes;

        string path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using FileStream file = new(
                path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, useAsync: true);
            await content.CopyToAsync(file, cancellationToken);
        }
        catch (IOException exception)
        {
            throw new StorageUnavailableException(exception);
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        string path = ResolvePath(key);
        try
        {
            Stream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
            return Task.FromResult(stream);
        }
        catch (FileNotFoundException exception)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageUnavailableException(exception);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        string path = ResolvePath(key);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            throw new StorageUnavailableException(exception);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        string relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new StorageUnavailableException();
        }

        return full;
    }
}
