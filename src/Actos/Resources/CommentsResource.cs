using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Nested comment lifecycle: creation under a post (optionally a reply to another comment),
/// tree listing, detail with ancestors, update, soft delete, and an actor's comment list.
/// </summary>
public sealed class CommentsResource
{
    private readonly Actos.Transport.Transport _transport;

    internal CommentsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Creates a comment on a post, or a reply when <paramref name="parentId"/> is set. An
    /// <c>Idempotency-Key</c> is generated automatically to prevent duplicate comments on retry.
    /// </summary>
    /// <param name="postId">The parent post id (<c>c_...</c>).</param>
    /// <param name="body">Comment body (markdown/HTML).</param>
    /// <param name="parentId">Optional parent comment id to make this a nested reply.</param>
    /// <param name="attachmentIds">Optional uploaded attachment ids.</param>
    public Task<ContentSummary> CreateAsync(
        string postId,
        string body,
        string? parentId = null,
        IReadOnlyCollection<string>? attachmentIds = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ContentSummary>(
            HttpMethod.Post,
            $"/posts/{postId}/comments",
            body: new CreateCommentRequest(
                Body: body,
                AttachmentIds: attachmentIds?.ToList(),
                ParentId: parentId),
            idempotencyKey: Guid.NewGuid().ToString(),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Lists a post's comment tree (one page; each node carries nested <c>Replies</c>). The
    /// <c>body_html</c> rendering is a separate flag, not a <c>fields</c> selection.
    /// </summary>
    /// <param name="postId">The parent post id.</param>
    /// <param name="sort"><see cref="Sort.New"/> or <see cref="Sort.Top"/>.</param>
    /// <param name="depth">Optional tree depth limit.</param>
    /// <param name="parent">Optional ancestor comment id to start the tree from.</param>
    /// <param name="bodyHtml">Request HTML-rendered bodies.</param>
    public Task<Page<CommentNodeResponse>> ListAsync(
        string postId,
        string sort = Sort.New,
        int? depth = null,
        string? parent = null,
        int? limit = null,
        string? cursor = null,
        bool bodyHtml = false,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommentThreadResponse>(
            HttpMethod.Get,
            $"/posts/{postId}/comments",
            QueryParams.Build(
                ("sort", sort),
                ("depth", depth),
                ("parent", parent),
                ("limit", limit),
                ("cursor", cursor),
                ("body_html", bodyHtml ? "true" : null)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<CommentNodeResponse>(t.Result.Comments, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Streams every comment node across pages, following the cursor chain.</summary>
    public IAsyncEnumerable<CommentNodeResponse> StreamAsync(
        string postId,
        string sort = Sort.New,
        int? depth = null,
        string? parent = null,
        int pageSize = PageExtensions.DefaultPageSize,
        bool bodyHtml = false,
        CancellationToken cancellationToken = default)
        => new Func<PageOptions, Task<Page<CommentNodeResponse>>>(
                options => ListAsync(postId, sort, depth, parent, options.Limit, options.Cursor, bodyHtml, cancellationToken))
            .StreamAsync(pageSize, cancellationToken);

    /// <summary>Fetches a single comment with its ancestor chain.</summary>
    public Task<CommentDetailResponse> GetAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommentDetailResponse>(
            HttpMethod.Get,
            $"/comments/{id}",
            cancellationToken: cancellationToken);

    /// <summary>Updates a comment's body.</summary>
    public Task<ContentSummary> UpdateAsync(string id, string body, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ContentSummary>(
            HttpMethod.Patch,
            $"/comments/{id}",
            body: new UpdateCommentRequest(body),
            cancellationToken: cancellationToken);

    /// <summary>Soft-deletes a comment (the author or a moderator).</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(HttpMethod.Delete, $"/comments/{id}", cancellationToken: cancellationToken);

    /// <summary>Lists a single actor's comments (one page).</summary>
    public Task<Page<ContentSummary>> ListByActorAsync(
        string username,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommentListResponse>(
            HttpMethod.Get,
            $"/actors/{username}/comments",
            QueryParams.Build(("limit", limit), ("cursor", cursor), ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ContentSummary>(t.Result.Comments, t.Result.NextCursor), cancellationToken);
}