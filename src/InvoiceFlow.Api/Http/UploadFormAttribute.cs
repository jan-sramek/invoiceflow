using InvoiceFlow.Api.Errors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InvoiceFlow.Api.Http;

/// <summary>
/// Reads the multipart form before model binding, so that a body over the limit fails as
/// <c>upload.too_large</c> and a broken one as <c>request.malformed</c> rather than as binding errors.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class UploadFormAttribute(long maxFileBytes) : Attribute, IAsyncResourceFilter
{
    // Room for the boundaries and part headers around the file
    private const long MultipartOverheadBytes = 64 * 1024;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        HttpContext httpContext = context.HttpContext;
        HttpRequest request = httpContext.Request;
        long maxBodyBytes = maxFileBytes + MultipartOverheadBytes;

        if (request.ContentLength > maxBodyBytes)
        {
            throw new UploadTooLargeException(maxFileBytes);
        }

        if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = maxBodyBytes;
        }

        if (request.HasFormContentType)
        {
            httpContext.Features.Set<IFormFeature>(
                new FormFeature(request, new FormOptions { MultipartBodyLengthLimit = maxBodyBytes }));
            try
            {
                await request.ReadFormAsync(httpContext.RequestAborted);
            }
            catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                throw new UploadTooLargeException(maxFileBytes);
            }
            catch (InvalidDataException)
            {
                throw new MalformedRequestException();
            }
        }

        await next();
    }
}

public sealed class MalformedRequestException()
    : InvoiceFlowException(ApiErrors.Request.Malformed, ErrorKind.Validation);
