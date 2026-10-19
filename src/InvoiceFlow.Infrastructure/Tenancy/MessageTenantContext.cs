using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Resolves the tenant from an ambient per-message override, falling back to
/// <see cref="TenancyOptions.DevelopmentTenantId"/> outside message handling.
/// </summary>
internal sealed class MessageTenantContext(IOptions<TenancyOptions> options) : ITenantContext
{
    private static readonly AsyncLocal<TenantId?> Override = new();

    public TenantId TenantId =>
        Override.Value
        ?? (options.Value.DevelopmentTenantId is { } id
            ? new TenantId(id)
            : throw new InvalidOperationException(
                $"{TenancyOptions.SectionName}:{nameof(TenancyOptions.DevelopmentTenantId)} is not configured."));

    public static IDisposable Use(TenantId tenantId)
    {
        TenantId? previous = Override.Value;
        Override.Value = tenantId;
        return new Reset(previous);
    }

    private sealed class Reset(TenantId? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}
