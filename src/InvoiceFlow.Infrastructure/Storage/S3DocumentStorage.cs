using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Application.Errors;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Storage;

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
                ServiceURL = settings.ServiceUrl!.ToString(),
                ForcePathStyle = true,
                // SeaweedFS ignores the region, but the request signature needs one.
                AuthenticationRegion = "us-east-1",
                // Newer SDKs add checksums by default that not every S3-compatible store accepts.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public Task StoreAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) =>
        CallAsync(
            () => _client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = content,
                    ContentType = contentType,
                    AutoCloseStream = false,
                },
                cancellationToken),
            cancellationToken);

    public async Task<Stream> OpenAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            GetObjectResponse response = await CallAsync(
                () => _client.GetObjectAsync(_bucket, key, cancellationToken), cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"No document is stored under '{key}'.", exception);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        CallAsync(() => _client.DeleteObjectAsync(_bucket, key, cancellationToken), cancellationToken);

    public void Dispose() => _client.Dispose();

    private static async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (IsUnavailable(exception, cancellationToken))
        {
            throw new StorageUnavailableException(exception);
        }
    }

    // Wrong credentials or a missing bucket are configuration errors, not outages, and stay unexpected.
    private static bool IsUnavailable(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        AmazonServiceException service => (int)service.StatusCode >= 500,
        AmazonClientException or HttpRequestException or IOException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };
}
