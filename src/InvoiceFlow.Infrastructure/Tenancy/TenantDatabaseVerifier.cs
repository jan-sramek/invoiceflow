using System.Collections.Concurrent;
using System.Data.Common;
using InvoiceFlow.Domain.Tenants;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Checks that a database really belongs to the tenant it was opened for, so a wrong catalog entry
/// fails loudly instead of showing another tenant's data.
/// </summary>
internal sealed class TenantDatabaseVerifier
{
    private readonly ConcurrentDictionary<(string Server, string Database), TenantId> _verified = new();

    /// <exception cref="InvalidOperationException">The database belongs to another tenant, or to none.</exception>
    public async Task VerifyAsync(DbConnection connection, TenantId tenantId, CancellationToken cancellationToken)
    {
        (string, string) database = (connection.DataSource, connection.Database);

        if (_verified.TryGetValue(database, out TenantId owner) && owner == tenantId)
        {
            return;
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT tenant_id FROM tenant_settings";

        List<Guid> owners = [];
        await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                owners.Add(reader.GetGuid(0));
            }
        }

        if (owners is not [var ownerId] || ownerId != tenantId.Value)
        {
            throw new InvalidOperationException(
                $"Database '{connection.Database}' on '{connection.DataSource}' does not belong to tenant {tenantId}.");
        }

        _verified[database] = tenantId;
    }
}
