using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Tests.Authentication;

public class DevelopmentUserTests
{
    [Theory]
    [InlineData("clerk", "dev-clerk", "Clara Clerk")]
    [InlineData("approver", "dev-approver", "Adam Approver")]
    [InlineData("finance", "dev-finance", "Fiona Finance")]
    public async Task Persona_SignsInAsThatUser(string persona, string id, string name)
    {
        await using TestEndpointsApi api = new();
        using HttpClient client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Development-User", persona);

        JsonElement user = await client.GetFromJsonAsync<JsonElement>("/test/me", TestContext.Current.CancellationToken);

        user.GetProperty("type").GetString().ShouldBe("User");
        user.GetProperty("id").GetString().ShouldBe(id);
        user.GetProperty("name").GetString().ShouldBe(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("admin")]
    [InlineData("Clerk")]
    public async Task NoKnownPersona_ReturnsUserRequired(string? persona)
    {
        await using TestEndpointsApi api = new();
        await ShouldBeUnauthorizedAsync(api, persona);
    }

    [Fact]
    public async Task Persona_WhenDevelopmentUsersAreOff_IsIgnored()
    {
        await using TestEndpointsApi api = new() { DevelopmentUsers = false };
        await ShouldBeUnauthorizedAsync(api, "clerk");
    }

    private static async Task ShouldBeUnauthorizedAsync(TestEndpointsApi api, string? persona)
    {
        using HttpClient client = api.CreateClient();
        if (persona is not null)
        {
            client.DefaultRequestHeaders.Add("X-Development-User", persona);
        }

        HttpResponseMessage response = await client.GetAsync("/test/me", TestContext.Current.CancellationToken);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        problem.GetProperty("code").GetString().ShouldBe("auth.user_required");
    }
}

[ApiController]
[Route("test/me")]
public sealed class CurrentUserTestController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public Actor Get([FromServices] ICurrentUser user) => user.Actor;
}
