using System.ComponentModel;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// Error codes raised by the HTTP layer. Codes are part of the API contract: never rename or reuse them.
/// The description is the constant title of the error response.
/// </summary>
public static class ApiErrors
{
    public static class Request
    {
        [Description("Malformed request")]
        public const string Malformed = "request.malformed";

        [Description("Invalid request")]
        public const string Invalid = "request.invalid";

        [Description("Resource not found")]
        public const string NotFound = "request.not_found";

        [Description("Method not allowed")]
        public const string MethodNotAllowed = "request.method_not_allowed";

        [Description("Unsupported media type")]
        public const string UnsupportedMediaType = "request.unsupported_media_type";
    }

    public static class Concurrency
    {
        /// <summary>
        /// A write came without the <c>ETag</c> the client read; this is a client bug, not a user error.
        /// </summary>
        [Description("Version required")]
        public const string IfMatchRequired = "concurrency.if_match_required";
    }

    public static class Validation
    {
        [Description("Invalid format")]
        public const string InvalidFormat = "validation.invalid_format";

        [Description("Unknown property")]
        public const string UnknownProperty = "validation.unknown_property";
    }

    public static class Server
    {
        [Description("Unexpected error")]
        public const string Unexpected = "server.unexpected";
    }
}
