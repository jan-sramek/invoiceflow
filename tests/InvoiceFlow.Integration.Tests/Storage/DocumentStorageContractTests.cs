using InvoiceFlow.Application.Abstractions.Storage;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Infrastructure.Storage;
using InvoiceFlow.Testing.Storage;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Integration.Tests.Storage;

/// <summary>
/// Behaviour every <see cref="IDocumentStorage"/> must keep, whether it is the in-memory fake or SeaweedFS.
/// </summary>
public abstract class DocumentStorageContractTests
{
    protected abstract IDocumentStorage Storage { get; }

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Store_Open_ReturnsTheSameBytes()
    {
        string key = UniqueKey();
        byte[] payload = "%PDF-1.4\nstorage-contract\n"u8.ToArray();

        await Storage.StoreAsync(key, new MemoryStream(payload), "application/pdf", CancellationToken);
        await using Stream opened = await Storage.OpenAsync(key, CancellationToken);

        (await ReadAllAsync(opened)).ShouldBe(payload);
    }

    [Fact]
    public async Task Store_SameKey_ReplacesTheDocument()
    {
        string key = UniqueKey();
        await Storage.StoreAsync(key, new MemoryStream("first"u8.ToArray()), "text/plain", CancellationToken);
        await Storage.StoreAsync(key, new MemoryStream("second"u8.ToArray()), "text/plain", CancellationToken);

        await using Stream opened = await Storage.OpenAsync(key, CancellationToken);
        (await ReadAllAsync(opened)).ShouldBe("second"u8.ToArray());
    }

    [Fact]
    public async Task Open_UnknownKey_ThrowsKeyNotFound()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => Storage.OpenAsync(UniqueKey(), CancellationToken));
    }

    [Fact]
    public async Task Delete_RemovesTheDocument()
    {
        string key = UniqueKey();
        await Storage.StoreAsync(key, new MemoryStream("gone"u8.ToArray()), "text/plain", CancellationToken);

        await Storage.DeleteAsync(key, CancellationToken);

        await Should.ThrowAsync<KeyNotFoundException>(() => Storage.OpenAsync(key, CancellationToken));
    }

    [Fact]
    public async Task Delete_UnknownKey_IsNotAnError()
    {
        await Storage.DeleteAsync(UniqueKey(), CancellationToken);
    }

    private static string UniqueKey() => $"tenants/test/documents/{Guid.CreateVersion7():D}";

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CancellationToken);
        return buffer.ToArray();
    }
}

public sealed class InMemoryDocumentStorageTests : DocumentStorageContractTests
{
    private readonly InMemoryDocumentStorage _storage = new();

    protected override IDocumentStorage Storage => _storage;

    [Fact]
    public async Task Unavailable_ThrowsStorageUnavailable()
    {
        _storage.Unavailable = true;

        await Should.ThrowAsync<StorageUnavailableException>(() =>
            _storage.StoreAsync("k", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken));
    }
}

[Collection(nameof(SeaweedFsTests))]
public sealed class SeaweedFsDocumentStorageTests(SeaweedFsFixture seaweed) : DocumentStorageContractTests
{
    protected override IDocumentStorage Storage => seaweed.Storage;

    [Fact]
    public async Task UnreachableEndpoint_ThrowsStorageUnavailable()
    {
        using var unreachable = new S3DocumentStorage(Options.Create(new DocumentStorageOptions
        {
            ServiceUrl = new Uri("http://127.0.0.1:1"),
            Bucket = SeaweedFsFixture.Bucket,
            AccessKey = SeaweedFsFixture.AccessKey,
            SecretKey = SeaweedFsFixture.SecretKey,
        }));

        await Should.ThrowAsync<StorageUnavailableException>(() =>
            unreachable.StoreAsync("k", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken));
    }
}
