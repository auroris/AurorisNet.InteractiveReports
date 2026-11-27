using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace InteractiveReport.AspNetCore;

/// <summary>
/// Request-side plumbing every HTTP client shares: projecting the exchange onto the
/// transport-neutral request context, refusing bodies not declared as JSON, and naming
/// downloads. Adapters differ in what they produce; how they read a request must not.
/// </summary>
public static class InteractiveReportHttpRequest
{
    /// <summary>
    /// Group endpoint filter that refuses, with 415 and <c>IR-1103</c>, a request to a
    /// body-reading route whose content type is not JSON. A route reads a body exactly when it
    /// declares <c>Accepts("application/json")</c>. Routing already answers another declared
    /// content type with an empty 415, but it lets a request with no <c>Content-Type</c> through,
    /// and such a POST is a simple request: a same-site page can send it with the caller's
    /// cookie and no CORS preflight.
    /// </summary>
    /// <param name="invocation">The current endpoint invocation.</param>
    /// <param name="next">The next filter or the route handler.</param>
    /// <returns>The coded 415 result, or the downstream result.</returns>
    internal static ValueTask<object?> RequireJsonBody(
        EndpointFilterInvocationContext invocation,
        EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<IAcceptsMetadata>() is null
            || http.Request.HasJsonContentType())
            return next(invocation);

        return ValueTask.FromResult<object?>(InteractiveReportHttpResult.Error(
            InteractiveReportErrorCodes.UnsupportedContentType,
            StatusCodes.Status415UnsupportedMediaType));
    }

    /// <summary>
    /// Projects the current HTTP exchange onto the request context the server boundary consumes.
    /// </summary>
    /// <param name="http">The current HTTP request and response context.</param>
    /// <returns>The request context carrying the principal, request services, and trace id.</returns>
    public static InteractiveReportRequestContext Context(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return new InteractiveReportRequestContext
        {
            User = http.User,
            RequestServices = http.RequestServices,
            TraceIdentifier = http.TraceIdentifier,
        };
    }

    /// <summary>
    /// Builds a filesystem-neutral download name: letters, digits, <c>.</c>, <c>-</c>, and <c>_</c>
    /// survive, everything else becomes <c>-</c>, and an empty stem falls back to <c>report</c>.
    /// </summary>
    /// <param name="stem">The human-readable name to sanitize.</param>
    /// <param name="extension">The extension to append, including its leading dot.</param>
    /// <returns>A name safe for a Content-Disposition header on every platform.</returns>
    public static string SafeFileName(string stem, string extension)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(extension);
        var safe = new string(stem.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-').ToArray()).Trim('.', '-');
        return (safe.Length == 0 ? "report" : safe) + extension;
    }
}
