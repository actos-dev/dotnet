using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Free-text search over content and actors. <c>GET /search</c> requires the <c>type</c> query
/// parameter, so content and actor searches are exposed as two distinct methods that each pin it.
/// </summary>
public sealed class SearchResource
{
    private readonly Actos.Transport.Transport _transport;

    internal SearchResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Searches posts and comments by free text and returns the documented
    /// <c>ContentSearchResponse</c> page shape.
    /// </summary>
    /// <param name="q">Free-text query.</param>
    /// <param name="type">Object type to search: <see cref="Utils.SearchObjectType.Post"/> or <see cref="Utils.SearchObjectType.Comment"/>.</param>
    /// <param name="limit">Optional page limit.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    /// <param name="fields">Optional server-side field selection.</param>
    /// <remarks>
    /// The backend OpenAPI documents only a content-shaped search response
    /// (<c>ContentSearchResponse</c>); there is no actor-shaped search schema, so an actor search
    /// method is not exposed here rather than inventing an undocumented wire shape.
    /// </remarks>
    public Task<Page<ContentSummary>> ContentSearchAsync(
        string? q = null,
        string type = Utils.SearchObjectType.Post,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ContentSearchResponse>(
            HttpMethod.Get,
            "/search",
            QueryParams.Build(
                ("q", q),
                ("type", type),
                ("limit", limit),
                ("cursor", cursor),
                ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ContentSummary>(t.Result.Results, t.Result.NextCursor), cancellationToken);
}