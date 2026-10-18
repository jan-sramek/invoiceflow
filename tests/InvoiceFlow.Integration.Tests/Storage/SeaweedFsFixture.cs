using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Integration.Tests.Storage;

/// <summary>
/// One SeaweedFS mini process for the storage tests, same image as the cluster StatefulSet.
/// </summary>
public sealed class SeaweedFsFixture : IAsyncLifetime
{
    private const string Image =
        "chrislusf/seaweedfs:4.48@sha256:4e61d15fd35994cb1e43e1e553dff106794841fd9a99ade2fc8c8bfce4d7872d";

    public const string AccessKey = "invoiceflow-test-access";
    public const string SecretKey = "invoiceflow-test-secret-key";
    public const string Bucket = "invoiceflow-documents";

    private readonly IContainer _container = new ContainerBuilder(Image)
        // 0.0.0.0 so the published port is reachable; the cluster StatefulSet uses 127.0.0.1 in-pod.
        .WithCommand(
            "mini",
            "-dir=/data",
            "-ip=0.0.0.0",
            $"-bucket={Bucket}",
            "-admin.ui=false",
            "-s3.autoCreateBucket=false",
            "-s3.allowDeleteBucketNotEmpty=false")
        .WithEnvironment("AWS_ACCESS_KEY_ID", AccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", SecretKey)
        .WithPortBinding(8333, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request.ForPort(8333).ForPath("/healthz")))
        .Build();

    private S3DocumentStorage _storage = null!;

    public IDocumentStorage Storage => _storage;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var serviceUrl = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8333)}");
        // mini -bucket creates the bucket for in-pod 127.0.0.1; with a published port we create it ourselves.
        await EnsureBucketAsync(serviceUrl);
        _storage = new S3DocumentStorage(Options.Create(new DocumentStorageOptions
        {
            ServiceUrl = serviceUrl,
            Bucket = Bucket,
            AccessKey = AccessKey,
            SecretKey = SecretKey,
        }));
    }

    private static async Task EnsureBucketAsync(Uri serviceUrl)
    {
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl.ToString(),
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict
            || exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _storage.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(SeaweedFsTests))]
public sealed class SeaweedFsTests : ICollectionFixture<SeaweedFsFixture>;
