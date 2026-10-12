using System.Data.Common;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Errors;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Points the tenant <c>DbContext</c> at the current tenant's database when its connection is first opened.
/// </summary>
internal sealed class TenantConnectionInterceptor(
    ITenantContext tenant,
    TenantConnectionResolver resolver,
    TenantDatabaseVerifier verifier) : DbConnectionInterceptor
{
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connection.ConnectionString))
        {
            connection.ConnectionString = await resolver.GetConnectionStringAsync(tenant.TenantId, cancellationToken);
        }

        return result;
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        verifier.VerifyAsync(connection, tenant.TenantId, cancellationToken);

    // Also called when the methods above throw; those exceptions are left as they are.
    public override Task ConnectionFailedAsync(
        DbConnection connection,
        ConnectionErrorEventData eventData,
        CancellationToken cancellationToken = default) =>
        eventData.Exception is NpgsqlException
            ? throw new TenantUnavailableException(eventData.Exception)
            : Task.CompletedTask;

    // Resolving needs I/O; a blocking open would tie up a thread per request while it waits.
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result) =>
        throw new InvalidOperationException("Tenant database connections must be opened asynchronously.");
}
