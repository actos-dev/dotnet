using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Tag directory, tag search and the post list behind a single tag. Handles the three endpoints
/// under <c>/tags</c>: the paginated tag list, the typeahead search, and the paginated posts for a
/// given tag name.
/// </summary>
public sealed class TagsResource
{
    private readonly Actos.Transport.Transport _transport;

    internal TagsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Fetches one page of the tag directory.
    /// </summary>
    /// <param name="limit">Optional page limit.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    public Task<Page<TagSummary>> ListAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<TagListResponse>(
            HttpMethod.Get,
            "/tags",
            QueryParams.Build(("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<TagSummary>(t.Result.Tags, t.Result.NextCursor), cancellationToken);

    /// <summary>
    /// Typeahead search over tag names.
    /// </summary>
    /// <param name="q">Tag name prefix to match against.</param>
    public Task<TagSearchResponse> SearchTagsAsync(string q, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<TagSearchResponse>(
            HttpMethod.Get,
            "/tags/search",
            QueryParams.Build(("q", q)),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Fetches one page of posts tagged with <paramref name="name"/>.
    /// </summary>
    /// <param name="name">Tag name whose posts to list.</param>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="limit">Optional page limit.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    /// <param name="fields">Optional server-side field selection.</param>
    public Task<Page<ContentSummary>> PostsAsync(
        string name,
        string sort = Utils.Sort.New,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<PostListResponse>(
            HttpMethod.Get,
            $"/tags/{name}/posts",
            QueryParams.Build(
                ("sort", sort),
                ("limit", limit),
                ("cursor", cursor),
                ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ContentSummary>(t.Result.Posts, t.Result.NextCursor), cancellationToken);
}