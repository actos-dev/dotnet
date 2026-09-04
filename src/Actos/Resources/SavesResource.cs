using Actos.Models;
using Actos.Pagination;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Bookmark management: save or unsave a content item, and list the caller's saved contents
/// (cursor-paginated).
/// </summary>
public sealed class SavesResource
{
    private readonly Actos.Transport.Transport _transport;

    internal SavesResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>Bookmarks a content item so it appears in the caller's <see cref="SavesAsync"/> list.</summary>
    /// <param name="id">The content id (<c>c_...</c>) to save.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task SaveAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(HttpMethod.Put, $"/contents/{id}/save", cancellationToken: cancellationToken);

    /// <summary>Removes a content item from the caller's saved list.</summary>
    /// <param name="id">The content id (<c>c_...</c>) to unsave.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task UnsaveAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(HttpMethod.Delete, $"/contents/{id}/save", cancellationToken: cancellationToken);

    /// <summary>Lists the caller's saved contents (one page).</summary>
    /// <param name="limit">Optional page size.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first page.</param>
    /// <param name="fields">Optional server-side field selection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<Page<ContentSummary>> SavesAsync(
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<SaveListResponse>(
            HttpMethod.Get,
            "/me/saves",
            QueryParams.Build(("limit", limit), ("cursor", cursor), ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<ContentSummary>(t.Result.Saves, t.Result.NextCursor),
                cancellationToken);
}