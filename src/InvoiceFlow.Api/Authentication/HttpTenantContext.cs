using System.Security.Claims;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Tenants;
using InvoiceFlow.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Api.Authentication;

/// <summary>
/// Prefers <c>tenant_id</c> on the access token. Falls back to
/// <see cref="TenancyOptions.DevelopmentTenantId"/> for local Development.
/// </summary>
internal sealed class HttpTenantContext(
    IHttpContextAccessor httpContextAccessor,
    IOptions<TenancyOptions> tenancy) : ITenantContext
{
    public TenantId TenantId
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            string? claim = user?.FindFirstValue(UserClaims.TenantId);
            if (Guid.TryParse(claim, out Guid fromToken))
            {
                return new TenantId(fromToken);
            }

            if (tenancy.Value.DevelopmentTenantId is { } fallback)
            {
                return new TenantId(fallback);
            }

            throw new InvalidOperationException(
                "No tenant_id claim on the current user, and Tenancy:DevelopmentTenantId is not configured.");
        }
    }
}
