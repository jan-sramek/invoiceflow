namespace InvoiceFlow.Api.Authentication;

/// <summary>
/// OpenID Connect settings for Keycloak. When <see cref="Authority"/> is empty, only development
/// users (if enabled) can sign in.
/// </summary>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// Metadata / JWKS base, e.g. the realm URL. Pods use the in-cluster Keycloak Service.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Issuer claim in access tokens (browser-facing Keycloak URL). Defaults to <see cref="Authority"/>.
    /// </summary>
    public string? ValidIssuer { get; set; }

    /// <summary>Expected <c>aud</c> claim; the SPA client maps this to <c>invoiceflow-api</c>.</summary>
    public string Audience { get; set; } = "invoiceflow-api";

    /// <summary>
    /// When true, <c>X-Development-User</c> personas work (local Windows / tests). Off in the cluster.
    /// </summary>
    public bool DevelopmentUsers { get; set; }

    /// <summary>Require HTTPS when fetching OIDC metadata. Local cluster Keycloak is HTTP in-cluster.</summary>
    public bool RequireHttpsMetadata { get; set; }

    public bool HasJwtBearer => !string.IsNullOrWhiteSpace(Authority);
}
