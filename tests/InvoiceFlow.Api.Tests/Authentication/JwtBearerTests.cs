using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace InvoiceFlow.Api.Tests.Authentication;

public class JwtBearerTests
{
    private static readonly Guid DevelopmentTenant = Guid.Parse("01a130f4-cc70-7419-9b64-feec3b283b96");

    [Fact]
    public async Task BearerToken_SignsInAsThatUser()
    {
        await using JwtTestApi api = new() { DevelopmentUsers = false };
        using HttpClient client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken("kc-clerk", "Clara Clerk", DevelopmentTenant));

        JsonElement user = await client.GetFromJsonAsync<JsonElement>("/test/me", TestContext.Current.CancellationToken);

        user.GetProperty("type").GetString().ShouldBe("User");
        user.GetProperty("id").GetString().ShouldBe("kc-clerk");
        user.GetProperty("name").GetString().ShouldBe("Clara Clerk");
    }

    [Fact]
    public async Task MissingBearer_WhenDevelopmentUsersOff_IsUnauthorized()
    {
        await using JwtTestApi api = new() { DevelopmentUsers = false };
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/test/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevelopmentHeader_StillWorks_WhenBothEnabled()
    {
        await using JwtTestApi api = new() { DevelopmentUsers = true };
        using HttpClient client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Development-User", "clerk");

        JsonElement user = await client.GetFromJsonAsync<JsonElement>("/test/me", TestContext.Current.CancellationToken);

        user.GetProperty("id").GetString().ShouldBe("dev-clerk");
    }

    private sealed class JwtTestApi : TestEndpointsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Authentication:DevelopmentUsers", DevelopmentUsers ? "true" : "false");
            builder.UseSetting("Authentication:Authority", TestJwt.Issuer);
            builder.UseSetting("Authentication:ValidIssuer", TestJwt.Issuer);
            builder.UseSetting("Authentication:Audience", TestJwt.Audience);
            builder.UseSetting("Authentication:RequireHttpsMetadata", "false");
            builder.UseSetting("Tenancy:DevelopmentTenantId", DevelopmentTenant.ToString());
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = null!;
                    options.MetadataAddress = null!;
                    options.RequireHttpsMetadata = false;
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = TestJwt.Issuer,
                    };
                    options.Configuration.SigningKeys.Add(TestJwt.SigningKey);
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidIssuer = TestJwt.Issuer;
                    options.TokenValidationParameters.ValidateAudience = true;
                    options.TokenValidationParameters.ValidAudience = TestJwt.Audience;
                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    options.TokenValidationParameters.IssuerSigningKey = TestJwt.SigningKey;
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.MapInboundClaims = false;
                });
            });
        }
    }
}
