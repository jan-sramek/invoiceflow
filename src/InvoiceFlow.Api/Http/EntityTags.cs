using InvoiceFlow.Api.Errors;
using InvoiceFlow.Domain.Errors;
using Microsoft.Net.Http.Headers;

namespace InvoiceFlow.Api.Http;

/// <summary>
/// Versions travel as strong entity tags: <c>ETag</c> on reads and successful writes, <c>If-Match</c> on writes.
/// </summary>
internal static class EntityTags
{
    public static EntityTagHeaderValue From(string version) => new($"\"{version}\"");

    /// <summary>
    /// Writes must name the one version they change; <c>*</c>, weak tags and lists name none.
    /// </summary>
    /// <exception cref="IfMatchRequiredException"><paramref name="ifMatch"/> names no single version.</exception>
    public static string RequiredVersion(string? ifMatch) =>
        EntityTagHeaderValue.TryParse(ifMatch, out EntityTagHeaderValue? tag)
        && tag is { IsWeak: false }
        && !tag.Equals(EntityTagHeaderValue.Any)
            ? tag.Tag.AsSpan(1, tag.Tag.Length - 2).ToString()
            : throw new IfMatchRequiredException();
}

public sealed class IfMatchRequiredException()
    : InvoiceFlowException(ApiErrors.Concurrency.IfMatchRequired, ErrorKind.PreconditionRequired);
