using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace Actos.Transport;

/// <summary>
/// Per-request retry policy. Set it on a request's <see cref="HttpRequestMessage.Options"/>
/// under <see cref="RetryHandler.RetryPolicyKey"/> to opt out of retries or to override the
/// retry count / idempotency classification for a single request.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>Whether retrying is enabled for this request. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum retries for this request. A value of <c>0</c> disables retries and <c>-1</c>
    /// (the default) defers to the handler's configured value.
    /// </summary>
    public int MaxRetries { get; init; } = -1;

    /// <summary>
    /// Overrides the auto-detected idempotency classification for 5xx retries.
    /// When <see langword="null"/> the handler treats safe/idempotent methods
    /// (GET/HEAD/OPTIONS/TRACE/PUT/DELETE) and any request carrying an
    /// <c>Idempotency-Key</c> header as safe to retry.
    /// </summary>
    public bool? Idempotent { get; init; }
}

/// <summary>
/// A <see cref="DelegatingHandler"/> that retries transport failures, 5xx responses and
/// HTTP 429 (honoring <c>Retry-After</c>), using exponential backoff with full jitter.
/// Other 4xx responses are never retried, and a 5xx is never retried for a mutating request
/// that lacks an <c>Idempotency-Key</c> header (double-write protection).
/// </summary>
public sealed class RetryHandler : DelegatingHandler
{
    /// <summary>Request option key under which an optional <see cref="RetryPolicy"/> is stored.</summary>
    public static readonly HttpRequestOptionsKey<RetryPolicy> RetryPolicyKey = new("Actos.RetryPolicy");

    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>Maximum number of retry attempts before giving up. 0 disables all retries.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Creates a new <see cref="RetryHandler"/> wrapping <paramref name="innerHandler"/>.</summary>
    public RetryHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var policy = request.Options.TryGetValue(RetryPolicyKey, out var requestedPolicy)
            ? requestedPolicy
            : new RetryPolicy();
        int maxRetries = policy.MaxRetries >= 0 ? policy.MaxRetries : MaxRetries;

        int attemptsUsed = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage? response = null;
            Exception? failure = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Distinguish a genuine caller cancellation (which must propagate) from a
                // client-side timeout (which is a retryable transport failure).
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw;
            }
            catch (HttpRequestException ex)
            {
                failure = ex;
            }
            catch (IOException ex)
            {
                failure = ex;
            }
            catch (SocketException ex)
            {
                failure = ex;
            }

            bool retryable = response is not null
                ? ShouldRetryResponse(response, request, policy, maxRetries)
                : policy.Enabled && failure is not null && maxRetries > 0;

            if (!retryable)
            {
                if (response is not null)
                {
                    return response;
                }

                throw failure!;
            }

            attemptsUsed++;
            if (attemptsUsed > maxRetries)
            {
                if (response is not null)
                {
                    return response;
                }

                throw failure!;
            }

            var wait = ComputeDelay(response, attemptsUsed);
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldRetryResponse(HttpResponseMessage response, HttpRequestMessage request, RetryPolicy policy, int maxRetries)
    {
        if (!policy.Enabled || maxRetries <= 0)
        {
            return false;
        }

        var status = (int)response.StatusCode;
        if (status == 429)
        {
            return true;
        }

        if (status is >= 500 and <= 599)
        {
            bool idempotent = policy.Idempotent ?? (IsSafeOrIdempotentMethod(request) || request.Headers.Contains(IdempotencyKeyHeader));
            return idempotent;
        }

        // All other 4xx (and anything else) are never retried.
        return false;
    }

    private static bool IsSafeOrIdempotentMethod(HttpRequestMessage request)
        => request.Method == HttpMethod.Get ||
           request.Method == HttpMethod.Head ||
           request.Method == HttpMethod.Options ||
           request.Method == HttpMethod.Trace ||
           request.Method == HttpMethod.Put ||
           request.Method == HttpMethod.Delete;

    private static TimeSpan ComputeDelay(HttpResponseMessage? response, int attempt)
    {
        if (response is not null && response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var honor = RetryAfterDelay(response.Headers.RetryAfter);
            if (honor is not null)
            {
                return honor.Value;
            }
        }

        // Exponential backoff with full jitter: delay = random in [0, min(2^(attempt-1)*base, cap)].
        double cap = Math.Min(Math.Pow(2, attempt - 1) * BaseDelay.TotalMilliseconds, MaxDelay.TotalMilliseconds);
        double jitter = Random.Shared.NextDouble() * cap;
        return TimeSpan.FromMilliseconds(jitter <= 0 ? 1 : jitter);
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
}