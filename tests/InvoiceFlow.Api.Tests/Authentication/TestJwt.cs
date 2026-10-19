using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceFlow.Api.Tests.Authentication;

internal static class TestJwt
{
    public const string Issuer = "https://invoiceflow.test/realms/invoiceflow";
    public const string Audience = "invoiceflow-api";

    private static readonly TimeProvider Clock = TimeProvider.System;

    public static readonly RsaSecurityKey SigningKey = CreateKey();
    public static readonly SigningCredentials SigningCredentials =
        new(SigningKey, SecurityAlgorithms.RsaSha256);

    public static string CreateToken(
        string subject,
        string name,
        Guid? tenantId = null,
        TimeSpan? lifetime = null)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, subject),
            new("name", name),
        ];
        if (tenantId is { } id)
        {
            claims.Add(new Claim("tenant_id", id.ToString()));
        }

        DateTimeOffset now = Clock.GetUtcNow();
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.Add(lifetime ?? TimeSpan.FromMinutes(5)).UtcDateTime,
            signingCredentials: SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RsaSecurityKey CreateKey()
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = "test-key" };
    }
}
