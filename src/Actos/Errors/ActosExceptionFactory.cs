namespace Actos.Errors;

/// <summary>
/// Maps an RFC 9457 error <c>code</c> to the correct sealed exception subclass.
/// Dispatch is always on <c>code</c>, never on HTTP status. Unknown codes fall back to
/// the base <see cref="ActosApiException"/> so the SDK never crashes on an unrecognized code.
/// </summary>
public static class ActosExceptionFactory
{
    /// <summary>Returns the sealed <see cref="ActosApiException"/> subclass for a problem-details error.</summary>
    /// <param name="code">The <c>code</c> field of the problem+json body.</param>
    /// <param name="statusCode">The HTTP status of the failed response.</param>
    /// <param name="title">Short human-readable summary.</param>
    /// <param name="detail">Occurrence-specific detail, if any.</param>
    /// <param name="type">Error-type URI, if any.</param>
    /// <param name="requestId">Correlation id, if any.</param>
    /// <param name="rawBody">The raw problem+json body received, if any.</param>
    /// <param name="retryAfter">Duration to wait before retrying (429 only), if any.</param>
    /// <param name="rateLimit">Rate-limit state observed on the response (429 only), if any.</param>
    /// <param name="innerException">The cause of this error, if any.</param>
    public static ActosApiException Create(
        string code,
        int statusCode,
        string? title,
        string? detail,
        string? type,
        string? requestId,
        string? rawBody,
        TimeSpan? retryAfter = null,
        Transport.RateLimit? rateLimit = null,
        Exception? innerException = null)
    {
        title ??= code;
        switch (code)
        {
            case "VALIDATION_FAILED":
                return new ActosValidationException(detail, title, type, requestId, rawBody, innerException);
            case "INVALID_CURSOR":
                return new ActosInvalidCursorException(detail, title, type, requestId, rawBody, innerException);
            case "MISSING_CREDENTIALS":
                return new ActosAuthenticationException(detail, title, type, requestId, rawBody, innerException);
            case "INVALID_KEY":
                return new ActosInvalidKeyException(detail, title, type, requestId, rawBody, innerException);
            case "FORBIDDEN":
                return new ActosForbiddenException(detail, title, type, requestId, rawBody, innerException);
            case "BANNED":
                return new ActosBannedException(detail, title, type, requestId, rawBody, innerException);
            case "NOT_FOUND":
                return new ActosNotFoundException(detail, title, type, requestId, rawBody, innerException);
            case "CONFLICT":
                return new ActosConflictException(detail, title, type, requestId, rawBody, innerException);
            case "GONE":
                return new ActosGoneException(detail, title, type, requestId, rawBody, innerException);
            case "UNSUPPORTED_MEDIA":
                return new ActosUnsupportedMediaException(detail, title, type, requestId, rawBody, innerException);
            case "RATE_LIMITED":
                return new ActosRateLimitException(detail, title, type, requestId, rawBody, retryAfter, rateLimit, innerException);
            case "INTERNAL":
            case "INTERNAL_SERVER_ERROR":
                return new ActosInternalException(statusCode, detail, title, type, requestId, rawBody, innerException);
            default:
                return new ActosApiException(statusCode, code, detail, title, type, requestId, rawBody, innerException);
        }
    }
}