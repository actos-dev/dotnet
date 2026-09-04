using System.Diagnostics;
using Actos.Resources;
using Actos.Transport;

namespace Actos;

/// <summary>
/// The single entry point of the Actos .NET SDK. Holds an <see cref="Actos.Transport.Transport"/> and exposes the
/// rate-limit state plus a raw HTTP escape hatch. Typed resource properties (<c>Auth</c>, <c>Posts</c>, …)
/// are introduced by later phases.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public sealed class ActosClient : IDisposable
{
    private readonly Actos.Transport.Transport _transport;
    private AuthResource? _auth;
    private ActorsResource? _actors;
    private PostsResource? _posts;
    private CommentsResource? _comments;
    private FeedResource? _feed;
    private SearchResource? _search;
    private TagsResource? _tags;
    private InboxResource? _inbox;
    private UploadsResource? _uploads;

    /// <summary>Authentication endpoints (register, keys, recovery).</summary>
    public AuthResource Auth => _auth ??= new AuthResource(_transport);

    /// <summary>Actor directory, profiles and follower lists.</summary>
    public ActorsResource Actors => _actors ??= new ActorsResource(_transport);

    /// <summary>Post lifecycle (create/fetch/update/delete).</summary>
    public PostsResource Posts => _posts ??= new PostsResource(_transport);

    /// <summary>Nested comment lifecycle.</summary>
    public CommentsResource Comments => _comments ??= new CommentsResource(_transport);

    /// <summary>Platform and follow-based feeds.</summary>
    public FeedResource Feed => _feed ??= new FeedResource(_transport);

    /// <summary>Free-text content search.</summary>
    public SearchResource Search => _search ??= new SearchResource(_transport);

    /// <summary>Tag directory, search and tagged post lists.</summary>
    public TagsResource Tags => _tags ??= new TagsResource(_transport);

    /// <summary>Notification inbox (list/read/readAll).</summary>
    public InboxResource Inbox => _inbox ??= new InboxResource(_transport);

    /// <summary>File upload lifecycle.</summary>
    public UploadsResource Uploads => _uploads ??= new UploadsResource(_transport);

    /// <summary>The normalized API base URL (no trailing slash).</summary>
    public string BaseUrl { get; }

    /// <summary>The API key in use, or <see langword="null"/> if none was configured.</summary>
    public string? ApiKey { get; }

    /// <summary>
    /// Rate-limit state from the most recently observed <c>X-RateLimit-*</c> response headers.
    /// <see langword="null"/> until the first response carrying them.
    /// </summary>
    public RateLimit? RateLimit => _transport.RateLimit;

    /// <summary>
    /// Creates a new <see cref="ActosClient"/>.
    /// </summary>
    /// <param name="apiKey">API key. When <see langword="null"/>, read from the <c>ACTOS_API_KEY</c> environment variable.</param>
    /// <param name="baseUrl">API base URL. When <see langword="null"/>, read from the <c>ACTOS_BASE_URL</c> environment variable, defaulting to <c>http://127.0.0.1:3100</c>.</param>
    /// <param name="options">Optional client configuration.</param>
    public ActosClient(string? apiKey = null, string? baseUrl = null, ActosClientOptions? options = null)
    {
        ApiKey = apiKey ?? Environment.GetEnvironmentVariable("ACTOS_API_KEY");
        BaseUrl = (baseUrl ?? Environment.GetEnvironmentVariable("ACTOS_BASE_URL") ?? "http://127.0.0.1:3100").TrimEnd('/');
        options ??= new ActosClientOptions();

        if (options.HttpClient is not null)
        {
            _transport = new Actos.Transport.Transport(options.HttpClient, BaseUrl, ApiKey, options.OwnsHttpClient);
        }
        else
        {
            _transport = new Actos.Transport.Transport(BaseUrl, ApiKey, options.HttpMessageHandler, options.MaxRetries, options.Timeout);
        }
    }

    /// <summary>
    /// Low-level escape hatch: sends an arbitrary <see cref="HttpRequestMessage"/> and returns the raw
    /// <see cref="HttpResponseMessage"/> without throwing on non-2xx statuses.
    /// </summary>
    public Task<HttpResponseMessage> RequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => _transport.RequestAsync(request, cancellationToken);

    /// <summary>Typed HTTP request that deserializes a 2xx body into <typeparamref name="TResponse"/> and throws a typed error otherwise.</summary>
    public Task<TResponse> RequestAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<TResponse>(method, path, query, body, idempotencyKey, cancellationToken);

    /// <summary>Masks the API key for safe display (<c>actos_***</c>).</summary>
    public static string MaskKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return "<null>";
        }

        return apiKey.StartsWith("actos_", StringComparison.Ordinal) ? "actos_***" : "***";
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ActosClient(BaseUrl={BaseUrl}, ApiKey={MaskKey(ApiKey)})";

    /// <inheritdoc />
    public void Dispose()
    {
        _transport.Dispose();
    }
}