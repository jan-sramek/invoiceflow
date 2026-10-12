using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Tenancy;

internal sealed class DevelopmentTenantContext(IOptions<TenancyOptions> options) : ITenantContext
{
    public TenantId TenantId { get; } = options.Value.DevelopmentTenantId is { } id
        ? new TenantId(id)
        : throw new InvalidOperationException(
            $"{TenancyOptions.SectionName}:{nameof(TenancyOptions.DevelopmentTenantId)} is not configured.");
}
