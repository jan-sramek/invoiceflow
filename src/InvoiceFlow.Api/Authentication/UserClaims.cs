namespace InvoiceFlow.Api.Authentication;

/// <summary>
/// OpenID Connect claim names, as Keycloak puts them in access tokens.
/// </summary>
internal static class UserClaims
{
    public const string Subject = "sub";
    public const string Name = "name";

    /// <summary>Hard-coded on development tokens until tenants come from membership.</summary>
    public const string TenantId = "tenant_id";
}
