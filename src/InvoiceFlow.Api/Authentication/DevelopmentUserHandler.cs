using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace InvoiceFlow.Api.Authentication;

/// <summary>
/// Signs requests in as one of three fixed personas named in <c>X-Development-User</c>, until Keycloak
/// replaces this scheme. It issues the same <c>sub</c> and <c>name</c> claims an access token will carry.
/// </summary>
internal sealed class DevelopmentUserHandler(
    IOptionsMonitor<DevelopmentUserOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<DevelopmentUserOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentUser";
    public const string HeaderName = "X-Development-User";

    private static readonly FrozenDictionary<string, (string Id, string Name)> Personas =
        new Dictionary<string, (string Id, string Name)>
        {
            ["clerk"] = ("dev-clerk", "Clara Clerk"),
            ["approver"] = ("dev-approver", "Adam Approver"),
            ["finance"] = ("dev-finance", "Fiona Finance"),
        }.ToFrozenDictionary();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Options.Enabled || !Request.Headers.TryGetValue(HeaderName, out StringValues header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Personas.TryGetValue(header.ToString(), out (string Id, string Name) persona))
        {
            return Task.FromResult(AuthenticateResult.Fail($"{HeaderName} names no known persona."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(UserClaims.Subject, persona.Id), new Claim(UserClaims.Name, persona.Name)],
            SchemeName,
            UserClaims.Name,
            roleType: null);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class DevelopmentUserOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Off unless configured; when off, the header is ignored and nobody is signed in.
    /// </summary>
    public bool Enabled { get; set; }
}
