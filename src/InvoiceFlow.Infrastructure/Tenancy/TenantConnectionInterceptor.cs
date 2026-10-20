using System.Data.Common;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Errors;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Polly.CircuitBreaker;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Points the tenant <c>DbContext</c> at the current tenant's database when its connection is first opened.
/// </summary>
internal sealed class TenantConnectionInterceptor(
    ITenantContext tenant,
    TenantConnectionResolver resolver,
    TenantDatabaseVerifier verifier,
    TenantDatabaseResilience resilience,
    TenantDatabaseMetrics metrics) : DbConnectionInterceptor
{
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        CircuitState state = resilience.StateOf(tenant.TenantId).CircuitState;
        if (state is CircuitState.Open or CircuitState.Isolated)
        {
            metrics.RecordBreakerRejection(tenant.TenantId);
            throw new TenantUnavailableException();
        }

        if (string.IsNullOrEmpty(connection.ConnectionString))
        {
            connection.ConnectionString = await resolver.GetConnectionStringAsync(tenant.TenantId, cancellationToken);
        }

        return result;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await resilience.RecordSuccessAsync(tenant.TenantId, cancellationToken);
        metrics.RecordOpen(tenant.TenantId);
        await verifier.VerifyAsync(connection, tenant.TenantId, cancellationToken);
    }

    // Also called when the methods above throw; those exceptions are left as they are.
    public override async Task ConnectionFailedAsync(
        DbConnection connection,
        ConnectionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Exception is not NpgsqlException)
        {
            return;
        }

        await resilience.RecordFailureAsync(tenant.TenantId, eventData.Exception, cancellationToken);
        metrics.RecordFailure(tenant.TenantId);
        throw new TenantUnavailableException(eventData.Exception);
    }

    // Resolving needs I/O; a blocking open would tie up a thread per request while it waits.
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result) =>
        throw new InvalidOperationException("Tenant database connections must be opened asynchronously.");
}
