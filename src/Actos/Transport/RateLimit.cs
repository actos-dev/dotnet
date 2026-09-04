using System.Globalization;
using System.Net.Http.Headers;

namespace Actos.Transport;

/// <summary>
/// Rate-limit state parsed from the <c>X-RateLimit-*</c> response headers.
/// The value observed on the most recent response wins (see <see cref="Transport.RateLimit"/>).
/// </summary>
public sealed class RateLimit
{
    /// <summary>Total requests permitted in the current window.</summary>
    public long? Limit { get; }

    /// <summary>Requests remaining in the current window.</summary>
    public long? Remaining { get; }

    /// <summary>When the current window resets, as a wall-clock moment.</summary>
    public DateTimeOffset? Reset { get; }

    private RateLimit(long? limit, long? remaining, DateTimeOffset? reset)
    {
        Limit = limit;
        Remaining = remaining;
        Reset = reset;
    }

    /// <summary>
    /// Builds a <see cref="RateLimit"/> from <c>X-RateLimit-Limit/-Remaining/-Reset</c>
    /// response headers. Returns <see langword="null"/> when none of the headers are present.
    /// </summary>
    /// <param name="headers">The headers of an HTTP response.</param>
    public static RateLimit? FromHeaders(HttpResponseHeaders headers)
    {
        long? limit = TryReadLong(headers, "X-RateLimit-Limit");
        long? remaining = TryReadLong(headers, "X-RateLimit-Remaining");
        DateTimeOffset? reset = null;
        if (TryReadLong(headers, "X-RateLimit-Reset", out long resetSeconds))
        {
            reset = DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
        }

        if (limit is null && remaining is null && reset is null)
        {
            return null;
        }

        return new RateLimit(limit, remaining, reset);
    }

    private static long? TryReadLong(HttpResponseHeaders headers, string name)
    {
        return TryReadLong(headers, name, out long value) ? value : null;
    }

    private static bool TryReadLong(HttpResponseHeaders headers, string name, out long value)
    {
        foreach (var header in headers)
        {
            if (!string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var item in header.Value)
            {
                if (long.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }
}