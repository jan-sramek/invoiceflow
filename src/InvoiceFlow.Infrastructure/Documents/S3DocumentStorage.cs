using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using InvoiceFlow.Application.Abstractions.Documents;
using InvoiceFlow.Application.Errors;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Documents;

internal sealed class S3DocumentStorage : IDocumentStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3DocumentStorage(IOptions<DocumentStorageOptions> options)
    {
        DocumentStorageOptions settings = options.Value;
        _bucket = settings.Bucket;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = settings.Region,
            });
    }

    public async Task PutAsync(
        string key, Stream content, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = content,
                    ContentType = contentType,
                    Headers = { ContentLength = sizeBytes },
                    AutoCloseStream = false,
                },
                cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (AmazonServiceException exception)
        {
            throw new StorageUnavailableException(exception);
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            GetObjectResponse response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (AmazonS3Exception exception)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (AmazonServiceException exception)
        {
            throw new StorageUnavailableException(exception);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            throw new StorageUnavailableException(exception);
        }
        catch (AmazonServiceException exception)
        {
            throw new StorageUnavailableException(exception);
        }
    }

    public void Dispose() => _client.Dispose();
}
