using System.Text.Json;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Persistence.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Builds a tenant's connection string from its catalog entry and its file in the mounted Secret.
/// </summary>
internal sealed class TenantConnectionResolver(
    IDbContextFactory<CatalogDbContext> catalog,
    IMemoryCache cache,
    IOptions<TenancyOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <exception cref="TenantUnavailableException">Unknown or inactive tenant, or no readable credentials.</exception>
    public async Task<string> GetConnectionStringAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        (await cache.GetOrCreateAsync(
            (nameof(TenantConnectionResolver), tenantId),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = options.Value.CacheDuration;
                return BuildAsync(tenantId, cancellationToken);
            }))!;

    private async Task<string> BuildAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        await using CatalogDbContext db = await catalog.CreateDbContextAsync(cancellationToken);
        TenantDatabase database = await db.Tenants
            .Where(t => t.Id == tenantId && t.Status == TenantStatus.Active)
            .Select(t => t.Database)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new TenantUnavailableException();

        Credentials credentials = await ReadCredentialsAsync(database.CredentialsKey, cancellationToken);

        return new NpgsqlConnectionStringBuilder
        {
            Host = database.Host,
            Port = database.Port,
            Database = database.DatabaseName,
            Username = credentials.Username,
            Password = credentials.Password,
            SslMode = database.SslMode,
            MaxPoolSize = options.Value.MaxPoolSize,
        }.ConnectionString;
    }

    private async Task<Credentials> ReadCredentialsAsync(string key, CancellationToken cancellationToken)
    {
        string path = Path.Combine(options.Value.CredentialsPath, key);

        try
        {
            await using FileStream file = File.OpenRead(path);
            Credentials? credentials =
                await JsonSerializer.DeserializeAsync<Credentials>(file, JsonOptions, cancellationToken);

            return credentials is { Username.Length: > 0, Password.Length: > 0 }
                ? credentials
                : throw new TenantUnavailableException();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new TenantUnavailableException(exception);
        }
    }

    private sealed record Credentials(string Username, string Password);
}
