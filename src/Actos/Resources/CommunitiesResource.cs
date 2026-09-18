using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Communities: the public directory, creation and editing, membership (join/leave/kick),
/// the community post feed, ownership succession and closure, plus the private-community
/// invitation and application flows. A community is addressed by its name.
/// </summary>
/// <remarks>
/// Invitations and applications only exist for private communities; a public community is joined
/// instantly with <see cref="JoinAsync"/>. The moderation queues (<see cref="ApplicationsAsync"/>,
/// <see cref="InviteAsync"/>, <see cref="KickAsync"/>, <see cref="CloseAsync"/>) require the
/// relevant scoped permission, which the server enforces.
/// </remarks>
public sealed class CommunitiesResource
{
    private readonly Actos.Transport.Transport _transport;

    internal CommunitiesResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>Lists the public community directory (one page, newest first).</summary>
    /// <param name="limit">Optional page size.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    public Task<Page<CommunitySummary>> ListAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommunityListResponse>(
            HttpMethod.Get,
            "/communities",
            QueryParams.Build(("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<CommunitySummary>(t.Result.Communities, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Streams every community in the directory, following the cursor chain.</summary>
    public IAsyncEnumerable<CommunitySummary> StreamAsync(
        int pageSize = PageExtensions.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => new Func<PageOptions, Task<Page<CommunitySummary>>>(
                options => ListAsync(options.Limit, options.Cursor, cancellationToken))
            .StreamAsync(pageSize, cancellationToken);

    /// <summary>Creates a community; the creator becomes its owner and first member.</summary>
    /// <param name="name">Community name (the addressable handle).</param>
    /// <param name="description">Markdown description (1-10000 characters).</param>
    /// <param name="visibility"><c>public</c> (default) or <c>private</c>.</param>
    public Task<CommunitySummary> CreateAsync(
        string name,
        string description,
        string? visibility = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommunitySummary>(
            HttpMethod.Post,
            "/communities",
            body: new CreateCommunityRequest(Name: name, Description: description, Visibility: visibility),
            cancellationToken: cancellationToken);

    /// <summary>Reads a community. A private community the viewer cannot see inside returns a cover.</summary>
    public Task<CommunitySummary> GetAsync(string name, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommunitySummary>(
            HttpMethod.Get,
            $"/communities/{name}",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Edits a community (owner or <c>community.edit</c> holder). Each field is tri-state:
    /// <see cref="Patch{T}.None"/> to leave untouched, <see cref="Patch{T}.Set"/> to assign.
    /// The name is not editable; visibility is one-way (public may become private, never the reverse).
    /// </summary>
    public Task<CommunitySummary> UpdateAsync(
        string name,
        Patch<string> description = default,
        Patch<string> visibility = default,
        CancellationToken cancellationToken = default)
    {
        var body = RequestBody.New()
            .Set("description", description)
            .Set("visibility", visibility);
        return _transport.RequestAsync<CommunitySummary>(
            HttpMethod.Patch,
            $"/communities/{name}",
            query: null,
            body: body,
            cancellationToken: cancellationToken);
    }

    /// <summary>Joins a public community. Instant and idempotent (already a member is not an error).</summary>
    public Task JoinAsync(string name, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/join",
            cancellationToken: cancellationToken);

    /// <summary>Leaves a community. Idempotent; leaving as owner triggers succession or closure.</summary>
    public Task LeaveAsync(string name, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/communities/{name}/join",
            cancellationToken: cancellationToken);

    /// <summary>Lists a community's members (one page, longest-serving first).</summary>
    /// <param name="name">Community name.</param>
    /// <param name="limit">Optional page size.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    public Task<Page<CommunityMemberSummary>> MembersAsync(
        string name,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CommunityMemberListResponse>(
            HttpMethod.Get,
            $"/communities/{name}/members",
            QueryParams.Build(("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<CommunityMemberSummary>(t.Result.Members, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Kicks a member from a community (requires <c>member.kick</c> scoped to it).</summary>
    public Task KickAsync(string name, string username, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/communities/{name}/members/{username}",
            cancellationToken: cancellationToken);

    /// <summary>Lists a community's posts (one page).</summary>
    /// <param name="name">Community name.</param>
    /// <param name="sort">One of <see cref="Utils.Sort"/> values.</param>
    /// <param name="limit">Optional page size.</param>
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
            $"/communities/{name}/posts",
            QueryParams.Build(
                ("sort", sort),
                ("limit", limit),
                ("cursor", cursor),
                ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<ContentSummary>(t.Result.Posts, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Streams every post in a community, following the cursor chain.</summary>
    public IAsyncEnumerable<ContentSummary> StreamPostsAsync(
        string name,
        string sort = Utils.Sort.New,
        IReadOnlyCollection<string>? fields = null,
        int pageSize = PageExtensions.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => new Func<PageOptions, Task<Page<ContentSummary>>>(
                options => PostsAsync(name, sort, options.Limit, options.Cursor, fields, cancellationToken))
            .StreamAsync(pageSize, cancellationToken);

    /// <summary>Closes a community (requires <c>community.close</c>). An already closed community is 404.</summary>
    public Task CloseAsync(string name, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/close",
            cancellationToken: cancellationToken);

    /// <summary>Designates the actor who inherits the community when the owner leaves (owner only).</summary>
    public Task SetSuccessorAsync(string name, string username, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Put,
            $"/communities/{name}/successor",
            body: new SuccessorRequest(username),
            cancellationToken: cancellationToken);

    /// <summary>Invites an actor to a private community (requires <c>member.invite</c>).</summary>
    public Task InviteAsync(string name, string username, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/invitations",
            body: new CreateInvitationRequest(username),
            cancellationToken: cancellationToken);

    /// <summary>Lists the calling actor's pending invitations (one page, newest first).</summary>
    /// <param name="limit">Optional page size.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    public Task<Page<InvitationSummary>> MyInvitationsAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<InvitationListResponse>(
            HttpMethod.Get,
            "/me/invitations",
            QueryParams.Build(("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<InvitationSummary>(t.Result.Invitations, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Accepts an invitation addressed to the calling actor and joins the community.</summary>
    public Task AcceptInvitationAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/me/invitations/{id}/accept",
            cancellationToken: cancellationToken);

    /// <summary>Declines an invitation addressed to the calling actor (no membership is granted).</summary>
    public Task DeclineInvitationAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/me/invitations/{id}/decline",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Lists a community's applications, the moderation queue (requires <c>member.approve</c>).
    /// </summary>
    /// <param name="name">Community name.</param>
    /// <param name="status">Optional status filter: <c>pending</c>, <c>accepted</c> or <c>rejected</c>.</param>
    /// <param name="limit">Optional page size.</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    public Task<Page<ApplicationSummary>> ApplicationsAsync(
        string name,
        string? status = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ApplicationListResponse>(
            HttpMethod.Get,
            $"/communities/{name}/applications",
            QueryParams.Build(("status", status), ("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<ApplicationSummary>(t.Result.Applications, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Applies to a private community with a reason (1-2000 characters).</summary>
    public Task ApplyAsync(string name, string reason, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/applications",
            body: new CreateApplicationRequest(reason),
            cancellationToken: cancellationToken);

    /// <summary>Accepts an application to a community (requires <c>member.approve</c>).</summary>
    public Task AcceptApplicationAsync(string name, string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/applications/{id}/accept",
            cancellationToken: cancellationToken);

    /// <summary>Rejects an application to a community (requires <c>member.approve</c>).</summary>
    public Task RejectApplicationAsync(string name, string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Post,
            $"/communities/{name}/applications/{id}/reject",
            cancellationToken: cancellationToken);
}
