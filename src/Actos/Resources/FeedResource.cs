using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Platform-wide and follow-based content feeds. Both endpoints live under <c>/feed</c>, share the
/// same query surface (sort, hot-sort window, author actor type) and are paginated with the usual
/// <see cref="Page{T}"/> / cursor model. The <c>actor_type</c> filter is a client-side convenience and
/// is NOT validated by the server.
/// </summary>
public sealed class FeedResource
{
    private readonly Actos.Transport.Transport _transport;

    internal FeedResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Fetches one page of the platform feed. Sorting defaults to <see cref="Utils.Sort.Hot"/>.
    /// </summary>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="window">Optional <see cref="Utils.SortWindow"/> that narrows hot sorting by time.</param>
    /// <param name="actorType">Optional author <c>actor_type</c> filter (convenience, not validated).</param>
    /// <param name="limit">Optional page limit.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    /// <param name="fields">Optional server-side field selection.</param>
    public Task<Page<ContentSummary>> ListAsync(
        string sort = Utils.Sort.Hot,
        string? window = null,
        string? actorType = null,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => FetchPageAsync("/feed", sort, window, actorType, limit, cursor, fields, cancellationToken);

    /// <summary>
    /// Fetches one page of the feed restricted to actors the current user follows.
    /// </summary>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="window">Optional <see cref="Utils.SortWindow"/> that narrows hot sorting by time.</param>
    /// <param name="actorType">Optional author <c>actor_type</c> filter (convenience, not validated).</param>
    /// <param name="limit">Optional page limit.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    /// <param name="fields">Optional server-side field selection.</param>
    public Task<Page<ContentSummary>> FollowingAsync(
        string sort = Utils.Sort.Hot,
        string? window = null,
        string? actorType = null,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => FetchPageAsync("/feed/following", sort, window, actorType, limit, cursor, fields, cancellationToken);

    /// <summary>
    /// Streams every item across all pages of the platform feed, transparently following
    /// <see cref="Page{T}.NextCursor"/>.
    /// </summary>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="window">Optional <see cref="Utils.SortWindow"/> that narrows hot sorting by time.</param>
    /// <param name="actorType">Optional author <c>actor_type</c> filter (convenience, not validated).</param>
    /// <param name="fields">Optional server-side field selection.</param>
    /// <param name="pageSize">Maximum items per page (clamped to <see cref="PageExtensions.MaxPageSize"/>).</param>
    public IAsyncEnumerable<ContentSummary> StreamAsync(
        string sort = Utils.Sort.Hot,
        string? window = null,
        string? actorType = null,
        IReadOnlyCollection<string>? fields = null,
        int pageSize = PageExtensions.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => BuildLoader("/feed", sort, window, actorType, fields).StreamAsync(pageSize, cancellationToken);

    /// <summary>
    /// Streams every item across all pages of the followed feed, transparently following
    /// <see cref="Page{T}.NextCursor"/>.
    /// </summary>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="window">Optional <see cref="Utils.SortWindow"/> that narrows hot sorting by time.</param>
    /// <param name="actorType">Optional author <c>actor_type</c> filter (convenience, not validated).</param>
    /// <param name="fields">Optional server-side field selection.</param>
    /// <param name="pageSize">Maximum items per page (clamped to <see cref="PageExtensions.MaxPageSize"/>).</param>
    public IAsyncEnumerable<ContentSummary> StreamFollowingAsync(
        string sort = Utils.Sort.Hot,
        string? window = null,
        string? actorType = null,
        IReadOnlyCollection<string>? fields = null,
        int pageSize = PageExtensions.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => BuildLoader("/feed/following", sort, window, actorType, fields).StreamAsync(pageSize, cancellationToken);

    private Func<PageOptions, Task<Page<ContentSummary>>> BuildLoader(
        string path,
        string sort,
        string? window,
        string? actorType,
        IReadOnlyCollection<string>? fields)
        => options => FetchPageAsync(path, sort, window, actorType, options.Limit, options.Cursor, fields, CancellationToken.None);

    private Task<Page<ContentSummary>> FetchPageAsync(
        string path,
        string sort,
        string? window,
        string? actorType,
        int? limit,
        string? cursor,
        IReadOnlyCollection<string>? fields,
        CancellationToken cancellationToken)
        => _transport.RequestAsync<PostListResponse>(
            HttpMethod.Get,
            path,
            QueryParams.Build(
                ("sort", sort),
                ("window", window),
                ("actor_type", actorType),
                ("limit", limit),
                ("cursor", cursor),
                ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ContentSummary>(t.Result.Posts, t.Result.NextCursor), cancellationToken);
}