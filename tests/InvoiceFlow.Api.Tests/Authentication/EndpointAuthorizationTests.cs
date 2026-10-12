using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Api.Tests.Authentication;

public class EndpointAuthorizationTests
{
    [Fact]
    [Trait("Requirement", "NFR-SEC-01")]
    public async Task Writes_AllRequireSignedInUser()
    {
        await using ApiFactory api = new();
        api.StartServer();

        RouteEndpoint[] writes =
        [
            .. from endpoint in api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
               where endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null
               let methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []
               where methods.Any(method => method != HttpMethods.Get)
               select endpoint,
        ];

        writes.ShouldNotBeEmpty();
        writes.Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(endpoint => endpoint.DisplayName)
            .ShouldBeEmpty();
    }
}
