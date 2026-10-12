using InvoiceFlow.Domain.Common;

namespace InvoiceFlow.Application.Abstractions.Context;

public interface ICurrentUser
{
    /// <summary>
    /// The signed-in user of the current request.
    /// </summary>
    /// <exception cref="Errors.UserRequiredException">Nobody is signed in.</exception>
    Actor Actor { get; }
}
