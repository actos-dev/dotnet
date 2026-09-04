namespace Actos;

/// <summary>
/// Configuration for an <see cref="ActosClient"/>. All members are optional.
/// </summary>
public sealed class ActosClientOptions
{
    /// <summary>Overall per-request timeout. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum retry count for transient failures. Defaults to 2. 0 disables retries.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// The innermost <see cref="HttpMessageHandler"/> used by the transport, or <see langword="null"/>
    /// to use the default. Inject a stub handler in tests.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>
    /// A fully configured <see cref="HttpClient"/>. When set, the transport uses it as-is and
    /// <see cref="HttpMessageHandler"/> is ignored.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Whether the transport should dispose <see cref="HttpClient"/> when it is disposed.</summary>
    public bool OwnsHttpClient { get; set; }
}