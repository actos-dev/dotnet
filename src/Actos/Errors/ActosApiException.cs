using System.Text;

namespace Actos.Errors;

/// <summary>
/// An error returned by the Actos API (an RFC 9457 <c>application/problem+json</c>
/// response). Raised whenever the server answers with a non-2xx status.
/// </summary>
/// <remarks>
/// Subclasses are dispatched by the <c>code</c> field of the problem body, never by
/// HTTP status (a <c>401</c> can mean either <c>MISSING_CREDENTIALS</c> or <c>INVALID_KEY</c>,
/// and a <c>403</c> either <c>FORBIDDEN</c> or <c>BANNED</c>). When the code is not recognized the
/// base <see cref="ActosApiException"/> is raised so that unknown errors never crash the caller.
/// </remarks>
public class ActosApiException : ActosException
{
    /// <summary>The HTTP status code of the failed response.</summary>
    public int StatusCode { get; }

    /// <summary>Machine-readable error code (for example <c>NOT_FOUND</c>).</summary>
    public string ErrorCode { get; }

    /// <summary>Human-readable, occurrence-specific explanation, when present.</summary>
    public string? Detail { get; }

    /// <summary>Short human-readable summary of the error.</summary>
    public string Title { get; }

    /// <summary>URI identifying the error type (points at documentation), when present.</summary>
    public string? Type { get; }

    /// <summary>Correlation id for support/debugging, when present.</summary>
    public string? RequestId { get; }

    /// <summary>The raw problem+json body as received, when one was returned.</summary>
    public string? RawBody { get; }

    /// <summary>
    /// Creates a new <see cref="ActosApiException"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status of the failed response.</param>
    /// <param name="errorCode">The machine-readable error code.</param>
    /// <param name="detail">Occurrence-specific detail, if any.</param>
    /// <param name="title">Short human-readable summary.</param>
    /// <param name="type">Error-type URI, if any.</param>
    /// <param name="requestId">Correlation id, if any.</param>
    /// <param name="rawBody">The raw problem+json body, if any.</param>
    /// <param name="innerException">The cause of this error, if any (if <see langword="null"/>, none).</param>
    internal ActosApiException(
        int statusCode,
        string errorCode,
        string? detail,
        string title,
        string? type,
        string? requestId,
        string? rawBody,
        Exception? innerException = null)
        : base(BuildMessage(statusCode, errorCode, detail, title, requestId), innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Detail = detail;
        Title = title;
        Type = type;
        RequestId = requestId;
        RawBody = rawBody;
    }

    /// <summary>
    /// Builds the standard message format: <c>[&lt;status&gt; &lt;CODE&gt;] &lt;detail|title&gt; (requestId=&lt;id&gt;)</c>.
    /// </summary>
    protected static string BuildMessage(int statusCode, string errorCode, string? detail, string title, string? requestId)
    {
        var text = string.IsNullOrWhiteSpace(detail) ? title : detail!;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Request failed.";
        }

        var builder = new StringBuilder();
        builder.Append('[')
            .Append(statusCode)
            .Append(' ')
            .Append(errorCode)
            .Append("] ")
            .Append(text);
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            builder.Append(" (requestId=").Append(requestId).Append(')');
        }

        return builder.ToString();
    }
}