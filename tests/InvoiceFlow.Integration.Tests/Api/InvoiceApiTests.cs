using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Integration.Tests.Persistence;
using InvoiceFlow.Testing.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Integration.Tests.Api;

/// <summary>
/// The API in memory for a new tenant, with helpers to seed invoices and to call it as a development persona.
/// </summary>
public abstract class InvoiceApiTests(PostgresFixture database) : IAsyncLifetime
{
    private TenantApi _api = null!;

    protected PostgresFixture Database => database;

    protected TestTenant Tenant { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected InMemoryDocumentStorage Storage => _api.Storage;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Tenant = await database.CreateTenantAsync();
        _api = new TenantApi(database, Tenant.Id);
        Client = _api.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _api.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected async Task<(InvoiceId Id, EntityTagHeaderValue ETag)> SeedAsync(Invoice invoice)
    {
        await AddAsync(Tenant.Id, invoice);
        return (invoice.Id, (await GetAsync(invoice.Id)).Headers.ETag.ShouldNotBeNull());
    }

    protected async Task AddAsync(TenantId tenant, Invoice invoice)
    {
        await using AsyncServiceScope scope = database.OpenScope(tenant);
        scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().Add(invoice);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken);
    }

    protected Task<HttpResponseMessage> GetAsync(InvoiceId id) =>
        Client.GetAsync($"/api/v1/invoices/{id}", CancellationToken);

    protected Task<HttpResponseMessage> PostAsync(
        InvoiceId id, string action, EntityTagHeaderValue? etag, object? body = null, string? persona = "clerk") =>
        SendAsync(HttpMethod.Post, $"/api/v1/invoices/{id}/{action}", etag, body, persona);

    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, EntityTagHeaderValue? etag, object? body = null, string? persona = "clerk")
    {
        using var request = new HttpRequestMessage(method, path);
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        if (persona is not null)
        {
            request.Headers.Add("X-Development-User", persona);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await Client.SendAsync(request, CancellationToken);
    }

    protected static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return document.RootElement.Clone();
    }
}
