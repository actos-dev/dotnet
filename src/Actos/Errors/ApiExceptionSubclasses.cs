namespace Actos.Errors;

// ---------------------------------------------------------------------------
// 400-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 400 — the request failed validation (<c>code</c> = <c>VALIDATION_FAILED</c>).</summary>
public sealed class ActosValidationException : ActosApiException
{
    internal ActosValidationException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(400, "VALIDATION_FAILED", detail, title, type, requestId, rawBody, inner)
    {
    }
}

/// <summary>HTTP 400 — an invalid pagination cursor was supplied (<c>code</c> = <c>INVALID_CURSOR</c>).</summary>
public sealed class ActosInvalidCursorException : ActosApiException
{
    internal ActosInvalidCursorException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(400, "INVALID_CURSOR", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 401-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 401 — credentials were not supplied at all (<c>code</c> = <c>MISSING_CREDENTIALS</c>).</summary>
public class ActosAuthenticationException : ActosApiException
{
    internal ActosAuthenticationException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(401, "MISSING_CREDENTIALS", detail, title, type, requestId, rawBody, inner)
    {
    }

    /// <summary>
    /// Interior constructor that allows a derived class to supply a different error code
    /// while keeping the HTTP status fixed at 401.
    /// </summary>
    protected ActosAuthenticationException(string code, string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(401, code, detail, title, type, requestId, rawBody, inner)
    {
    }
}

/// <summary>HTTP 401 — a key was supplied but was invalid (<c>code</c> = <c>INVALID_KEY</c>).</summary>
public sealed class ActosInvalidKeyException : ActosAuthenticationException
{
    internal ActosInvalidKeyException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base("INVALID_KEY", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 403-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 403 — authenticated but not permitted (<c>code</c> = <c>FORBIDDEN</c>).</summary>
public class ActosForbiddenException : ActosApiException
{
    internal ActosForbiddenException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(403, "FORBIDDEN", detail, title, type, requestId, rawBody, inner)
    {
    }

    /// <summary>
    /// Interior constructor that allows a derived class to supply a different error code
    /// while keeping the HTTP status fixed at 403.
    /// </summary>
    protected ActosForbiddenException(string code, string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(403, code, detail, title, type, requestId, rawBody, inner)
    {
    }
}

/// <summary>HTTP 403 — the actor is banned (<c>code</c> = <c>BANNED</c>).</summary>
public sealed class ActosBannedException : ActosForbiddenException
{
    internal ActosBannedException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base("BANNED", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 404-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 404 — the target resource does not exist (<c>code</c> = <c>NOT_FOUND</c>).</summary>
public sealed class ActosNotFoundException : ActosApiException
{
    internal ActosNotFoundException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(404, "NOT_FOUND", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 409-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 409 — the request conflicts with the current server state (<c>code</c> = <c>CONFLICT</c>).</summary>
public sealed class ActosConflictException : ActosApiException
{
    internal ActosConflictException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(409, "CONFLICT", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 410-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 410 — the resource existed but is gone (soft-deleted post) (<c>code</c> = <c>GONE</c>).</summary>
public sealed class ActosGoneException : ActosApiException
{
    internal ActosGoneException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(410, "GONE", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 415-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 415 — an unsupported media type was supplied (<c>code</c> = <c>UNSUPPORTED_MEDIA</c>).</summary>
public sealed class ActosUnsupportedMediaException : ActosApiException
{
    internal ActosUnsupportedMediaException(string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(415, "UNSUPPORTED_MEDIA", detail, title, type, requestId, rawBody, inner)
    {
    }
}

// ---------------------------------------------------------------------------
// 429-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 429 — the request was rate limited (<c>code</c> = <c>RATE_LIMITED</c>).</summary>
public sealed class ActosRateLimitException : ActosApiException
{
    /// <summary>Duration to wait before retrying, from the <c>Retry-After</c> header when present.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Rate-limit state observed on the response, when the headers were present.</summary>
    public Transport.RateLimit? RateLimit { get; }

    internal ActosRateLimitException(
        string? detail,
        string title,
        string? type,
        string? requestId,
        string? rawBody,
        TimeSpan? retryAfter,
        Transport.RateLimit? rateLimit,
        Exception? inner = null)
        : base(429, "RATE_LIMITED", detail, title, type, requestId, rawBody, inner)
    {
        RetryAfter = retryAfter;
        RateLimit = rateLimit;
    }
}

// ---------------------------------------------------------------------------
// 5xx-series
// ---------------------------------------------------------------------------

/// <summary>HTTP 5xx — an internal server error (<c>code</c> = <c>INTERNAL</c>).</summary>
public sealed class ActosInternalException : ActosApiException
{
    internal ActosInternalException(int statusCode, string? detail, string title, string? type, string? requestId, string? rawBody, Exception? inner = null)
        : base(statusCode, "INTERNAL", detail, title, type, requestId, rawBody, inner)
    {
    }
}