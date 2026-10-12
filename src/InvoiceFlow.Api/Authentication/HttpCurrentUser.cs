using System.Security.Claims;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Api.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Actor Actor
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;

            return user?.Identity?.IsAuthenticated == true
                && user.FindFirstValue(UserClaims.Subject) is { Length: > 0 } subject
                && user.FindFirstValue(UserClaims.Name) is { Length: > 0 } name
                    ? Actor.User(subject, name)
                    : throw new UserRequiredException();
        }
    }
}
