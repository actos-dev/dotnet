using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Post lifecycle: creation (with automatic idempotency protection), fetch, update (tri-state
/// PATCH), soft delete, and an actor's post list.
/// </summary>
public sealed class PostsResource
{
    private readonly Actos.Transport.Transport _transport;

    internal PostsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Creates a post. An <c>Idempotency-Key</c> is generated automatically (UUID) so a timed-out
    /// retry can never create a duplicate; pass <paramref name="idempotencyKey"/> to set your own.
    /// When <paramref name="files"/> is given the request goes out as <c>multipart/form-data</c>
    /// (a <c>payload</c> part plus up to <see cref="MultipartRequest.MaxFiles"/> <c>files</c> parts);
    /// otherwise the body stays plain <c>application/json</c>.
    /// </summary>
    /// <param name="title">Post title.</param>
    /// <param name="body">Markdown/HTML body.</param>
    /// <param name="tags">Optional tag names.</param>
    /// <param name="files">Optional images to attach (up to <see cref="MultipartRequest.MaxFiles"/>).</param>
    /// <param name="idempotencyKey">Override for the automatic idempotency key.</param>
    /// <param name="community">Optional community name to post into; the author must be a member.</param>
    /// <param name="crossPostSource">Optional content id (<c>c_...</c>) to cross-post; when set, <paramref name="title"/> and <paramref name="body"/> are accepted but ignored by the server.</param>
    public async Task<ContentSummary> CreateAsync(
        string title,
        string body,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyCollection<FileUpload>? files = null,
        string? idempotencyKey = null,
        string? community = null,
        string? crossPostSource = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new CreatePostRequest(
            Body: body,
            Title: title,
            Community: community,
            CrossPostSource: crossPostSource,
            Tags: tags?.ToList());
        var key = idempotencyKey ?? Guid.NewGuid().ToString();

        if (files is not { Count: > 0 })
        {
            return await _transport.RequestAsync<ContentSummary>(
                HttpMethod.Post,
                "/posts",
                body: payload,
                idempotencyKey: key,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        using var form = MultipartRequest.BuildPayloadWithFiles(payload, files);
        return await _transport.RequestAsync<ContentSummary>(
            HttpMethod.Post,
            "/posts",
            form,
            idempotencyKey: key,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches a single post.</summary>
    /// <param name="id">Post id (<c>c_...</c>).</param>
    /// <param name="fields">Optional server-side field selection.</param>
    public Task<ContentSummary> GetAsync(
        string id,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ContentSummary>(
            HttpMethod.Get,
            $"/posts/{id}",
            QueryParams.Build(("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Partially updates a post. Each of <paramref name="title"/> and <paramref name="body"/> is
    /// tri-state: <see cref="Patch{T}.None"/> to leave untouched, <see cref="Patch{T}.Set"/> to set.
    /// </summary>
    public Task<ContentSummary> UpdateAsync(
        string id,
        Patch<string> title = default,
        Patch<string> body = default,
        CancellationToken cancellationToken = default)
    {
        var patchBody = RequestBody.New().Set("title", title).Set("body", body);
        return _transport.RequestAsync<ContentSummary>(
            HttpMethod.Patch,
            $"/posts/{id}",
            query: null,
            body: patchBody,
            cancellationToken: cancellationToken);
    }

    /// <summary>Soft-deletes a post (the author or a moderator).</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(HttpMethod.Delete, $"/posts/{id}", cancellationToken: cancellationToken);

    /// <summary>Lists a single actor's posts (one page).</summary>
    public Task<Page<ContentSummary>> ListByActorAsync(
        string username,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<PostListResponse>(
            HttpMethod.Get,
            $"/actors/{username}/posts",
            QueryParams.Build(("limit", limit), ("cursor", cursor), ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ContentSummary>(t.Result.Posts, t.Result.NextCursor), cancellationToken);
}