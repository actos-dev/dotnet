using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Actos.Errors;

namespace Actos.Transport;

/// <summary>
/// Low-level HTTP core for the Actos SDK: builds requests, attaches credentials, serializes
/// bodies, parses rate-limit headers and maps non-2xx responses to typed <see cref="ActosApiException"/>
/// subclasses.
/// </summary>
public sealed class Transport : IDisposable
{
    private const string HeaderIdempotencyKey = "Idempotency-Key";
    private const string HeaderRateLimitLimit = "X-RateLimit-Limit";
    private const string HeaderRateLimitRemaining = "X-RateLimit-Remaining";
    private const string HeaderRateLimitReset = "X-RateLimit-Reset";
    private const string HeaderRequestId = "x-request-id";
    private const int DefaultTimeoutSeconds = 30;

    private static readonly string UserAgent = "actos-dotnet/" + SdkInfo.Version;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    /// <summary>
    /// Rate-limit state from the most recently observed <c>X-RateLimit-*</c> response headers
    /// (last response wins). <see langword="null"/> until the first response carrying them.
    /// </summary>
    public RateLimit? RateLimit { get; private set; }

    /// <summary>
    /// Creates a transport that owns its own <see cref="HttpClient"/>, wrapping an optional
    /// caller-supplied inner handler with a <see cref="RetryHandler"/>.
    /// </summary>
    /// <param name="baseUrl">The Actos API origin, with no trailing slash.</param>
    /// <param name="apiKey">The API key used for <c>Authorization: Bearer</c>, or <see langword="null"/>.</param>
    /// <param name="innerHandler">The innermost handler (for injection in tests). When <see langword="null"/> a default <see cref="SocketsHttpHandler"/> is used.</param>
    /// <param name="maxRetries">Maximum retry count (see <see cref="RetryHandler"/>). 0 disables retries.</param>
    /// <param name="timeout">Overall request timeout. Defaults to 30 seconds.</param>
    public Transport(
        string baseUrl,
        string? apiKey,
        HttpMessageHandler? innerHandler = null,
        int maxRetries = 2,
        TimeSpan? timeout = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        var retry = new RetryHandler(innerHandler ?? new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            MaxRetries = maxRetries,
        };
        _httpClient = new HttpClient(retry)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds),
        };
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a transport around a caller-supplied <see cref="HttpClient"/>. Callers are
    /// responsible for configuring retries, timeouts and the base address themselves.
    /// </summary>
    /// <param name="httpClient">The fully configured <see cref="HttpClient"/> to use.</param>
    /// <param name="baseUrl">The Actos API origin, with no trailing slash.</param>
    /// <param name="apiKey">The API key used for <c>Authorization: Bearer</c>, or <see langword="null"/>.</param>
    /// <param name="ownsHttpClient">Whether the transport should dispose the supplied client.</param>
    public Transport(HttpClient httpClient, string baseUrl, string? apiKey, bool ownsHttpClient = false)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
    }

    /// <summary>
    /// Sends a request and deserializes a successful (2xx) response body into <typeparamref name="TResponse"/>.
    /// Non-2xx responses throw the appropriate typed <see cref="ActosApiException"/>.
    /// </summary>
    /// <typeparam name="TResponse">The response DTO type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The API path, for example <c>/posts</c>.</param>
    /// <param name="query">Pre-encoded query parameters (both keys and values) appended to the URL.</param>
    /// <param name="body">The request body to serialize with <see cref="Json.Wire"/>, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">An <c>Idempotency-Key</c> header value, allowing safe retries of mutating requests.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<TResponse> RequestAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(method, path, query, body, idempotencyKey);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request whose body is already fully formed as an <see cref="HttpContent"/> (for example
    /// a <c>multipart/form-data</c> payload or a raw stream) and deserializes a successful (2xx)
    /// response body into <typeparamref name="TResponse"/>. Non-2xx responses throw the appropriate
    /// typed <see cref="ActosApiException"/>. Unlike the JSON <c>body</c> overload, the content is
    /// passed through untouched — callers set its own content type (e.g. <c>multipart/form-data</c>).
    /// </summary>
    /// <typeparam name="TResponse">The response DTO type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The API path, for example <c>/uploads</c>.</param>
    /// <param name="content">The raw request body content, sent verbatim.</param>
    /// <param name="query">Pre-encoded query parameters appended to the URL.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<TResponse> RequestAsync<TResponse>(
        HttpMethod method,
        string path,
        HttpContent content,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path, query))
        {
            Content = content,
        };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Low-level escape hatch: sends an arbitrary <see cref="HttpRequestMessage"/> and returns the raw
    /// <see cref="HttpResponseMessage"/> without throwing on non-2xx statuses. Credentials, <c>User-Agent</c>
    /// and <c>Accept</c> headers are applied if the caller has not already set them.
    /// </summary>
    public async Task<HttpResponseMessage> RequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ApplyDefaults(request);
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        CaptureRateLimit(response);
        return response;
    }

    /// <summary>
    /// Sends a request and returns without a body (used for <c>204 No Content</c> endpoints).
    /// Non-2xx responses throw the appropriate typed <see cref="ActosApiException"/>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The API path, for example <c>/contents/{id}/save</c>.</param>
    /// <param name="query">Pre-encoded query parameters appended to the URL.</param>
    /// <param name="body">The request body to serialize, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">An optional <c>Idempotency-Key</c> header value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task RequestNoContentAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(method, path, query, body, idempotencyKey);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowForNonSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ApplyDefaults(request);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The HttpClient timeout fired; this is a transport failure, not a caller cancel.
            throw new ActosTimeoutException($"The request to {request.RequestUri} timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ActosConnectionException($"Could not connect to {request.RequestUri}.", ex);
        }

        CaptureRateLimit(response);
        return response;
    }

    private HttpRequestMessage BuildRequest(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query,
        object? body,
        string? idempotencyKey)
    {
        var uri = BuildUri(path, query);
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            // JsonNode bodies (PATCH/partial) are serialized with Json.Request so that explicit
            // null members (tri-state "unset") survive; typed DTOs use Json.Wire (omit nulls).
            var json = JsonSerializer.Serialize(body, body is JsonNode ? Json.Request : Json.Wire);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation(HeaderIdempotencyKey, idempotencyKey);
        }

        ApplyDefaults(request);
        return request;
    }

    private void ApplyDefaults(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is null && !string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        if (!request.Headers.Accept.Any(h => string.Equals(h.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        request.Headers.UserAgent.TryParseAdd(UserAgent);
    }

    private Uri BuildUri(string path, IReadOnlyDictionary<string, string>? query)
    {
        var route = path.StartsWith('/') ? path : "/" + path;
        var full = _baseUrl + route;
        if (query is { Count: > 0 })
        {
            var separator = full.IndexOf('?') >= 0 ? '&' : '?';
            var sb = new StringBuilder(full);
            sb.Append(separator);
            var first = true;
            foreach (var pair in query)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                first = false;
                sb.Append(pair.Key).Append('=').Append(pair.Value);
            }

            full = sb.ToString();
        }

        return new Uri(full, UriKind.Absolute);
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowForNonSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (typeof(TResponse) == typeof(string))
        {
            return (TResponse)(object)(json ?? string.Empty);
        }

        if (typeof(TResponse) == typeof(JsonElement))
        {
            return (TResponse)(object)JsonDocument.Parse(json).RootElement;
        }

        var value = JsonSerializer.Deserialize<TResponse>(json, Json.Wire);
        if (value is null)
        {
            throw new JsonException($"Response body for {typeof(TResponse).Name} was null.");
        }

        return value;
    }

    private static async Task ThrowForNonSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        int status = (int)response.StatusCode;
        string? rawBody = response.Content is not null
            ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : null;

        string? requestId = null;
        if (response.Headers.TryGetValues(HeaderRequestId, out var ids))
        {
            requestId = ids.FirstOrDefault();
        }

        var code = "UNKNOWN";
        var title = string.Empty;
        string? detail = null;
        string? type = null;
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            ParseProblemBody(rawBody, ref code, ref title, ref detail, ref type, ref requestId);
        }

        var retryAfter = RetryAfterDelay(response.Headers.RetryAfter);
        var rateLimit = RateLimit.FromHeaders(response.Headers);

        throw ActosExceptionFactory.Create(code, status, title, detail, type, requestId, rawBody, retryAfter, rateLimit);
    }

    private static void ParseProblemBody(string rawBody, ref string code, ref string title, ref string? detail, ref string? type, ref string? requestId)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
            {
                code = codeEl.GetString() ?? code;
            }

            if (root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
            {
                title = titleEl.GetString() ?? title;
            }

            if (root.TryGetProperty("detail", out var detailEl) && detailEl.ValueKind == JsonValueKind.String)
            {
                detail = detailEl.GetString();
            }

            if (root.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                type = typeEl.GetString();
            }

            if (string.IsNullOrEmpty(requestId) &&
                root.TryGetProperty("request_id", out var requestIdEl) &&
                requestIdEl.ValueKind == JsonValueKind.String)
            {
                requestId = requestIdEl.GetString();
            }
        }
        catch (JsonException)
        {
            // If the body is not valid JSON, fall back to whatever we could parse.
        }
    }

    private void CaptureRateLimit(HttpResponseMessage response)
    {
        var rateLimit = RateLimit.FromHeaders(response.Headers);
        if (rateLimit is not null)
        {
            RateLimit = rateLimit;
        }
    }

    private static TimeSpan? RetryAfterDelay(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var deltaTime = date - DateTimeOffset.UtcNow;
            return deltaTime > TimeSpan.Zero ? deltaTime : TimeSpan.Zero;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}