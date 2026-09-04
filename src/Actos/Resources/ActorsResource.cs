using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Resources;

/// <summary>
/// Actor directory and profile endpoints: discovery, profile lookup, self-update, deletion, and
/// follower/following lists.
/// </summary>
public sealed class ActorsResource
{
    private readonly Actos.Transport.Transport _transport;

    internal ActorsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Lists actors in the discovery directory (one page). <paramref name="type"/> / <paramref name="sort"/>
    /// filter the directory; use <see cref="StreamAsync"/> for transparent cursor walking.
    /// </summary>
    public Task<Page<ActorSummary>> ListAsync(
        string? type = null,
        string sort = Sort.New,
        int? limit = null,
        string? cursor = null,
        IReadOnlyCollection<string>? fields = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ActorListResponse>(
            HttpMethod.Get,
            "/actors",
            QueryParams.Build(("type", type), ("sort", sort), ("limit", limit), ("cursor", cursor), ("fields", QueryParams.Fields(fields))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => new Page<ActorSummary>(t.Result.Actors, t.Result.NextCursor), cancellationToken);

    /// <summary>Streams every actor, following the cursor chain until exhausted.</summary>
    public IAsyncEnumerable<ActorSummary> StreamAsync(
        string? type = null,
        string sort = Sort.New,
        int pageSize = PageExtensions.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => new Func<PageOptions, Task<Page<ActorSummary>>>(
                options => ListAsync(type, sort, options.Limit, options.Cursor, null, cancellationToken))
            .StreamAsync(pageSize, cancellationToken);

    /// <summary>Fetches a public actor profile (includes per-actor stats).</summary>
    public Task<ActorProfileResponse> GetAsync(string username, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ActorProfileResponse>(
            HttpMethod.Get,
            $"/actors/{username}",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Updates the current actor's profile. Each of <paramref name="displayName"/>, <paramref name="bio"/>
    /// and <paramref name="avatar"/> is tri-state: <see cref="Patch{T}.None"/> to leave untouched,
    /// <see cref="Patch{T}.Set"/> to assign, <see cref="Patch{T}.Unset"/> to clear with an explicit null.
    /// </summary>
    public Task<UpdateProfileResponse> UpdateMeAsync(
        Patch<string> displayName = default,
        Patch<string> bio = default,
        Patch<string> avatar = default,
        CancellationToken cancellationToken = default)
    {
        var body = RequestBody.New()
            .Set("display_name", displayName)
            .Set("bio", bio)
            .Set("avatar", avatar);
        return _transport.RequestAsync<UpdateProfileResponse>(
            HttpMethod.Patch,
            "/actors/me",
            query: null,
            body: body,
            cancellationToken: cancellationToken);
    }

    /// <summary>Permanently deletes the current account; a recovery code is required as proof.</summary>
    public Task DeleteMeAsync(string recoveryCode, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            "/actors/me",
            body: new DeleteAccountRequest(recoveryCode),
            cancellationToken: cancellationToken);

    /// <summary>Lists an actor's followers (one page).</summary>
    public Task<Page<ActorSummary>> FollowersAsync(
        string username,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => MapListAsync(
            () => _transport.RequestAsync<ActorListResponse>(
                HttpMethod.Get,
                $"/actors/{username}/followers",
                QueryParams.Build(("limit", limit), ("cursor", cursor)),
                cancellationToken: cancellationToken));

    /// <summary>Lists the actors an actor follows (one page).</summary>
    public Task<Page<ActorSummary>> FollowingAsync(
        string username,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => MapListAsync(
            () => _transport.RequestAsync<ActorListResponse>(
                HttpMethod.Get,
                $"/actors/{username}/following",
                QueryParams.Build(("limit", limit), ("cursor", cursor)),
                cancellationToken: cancellationToken));

    private static async Task<Page<ActorSummary>> MapListAsync(Func<Task<ActorListResponse>> loader)
    {
        var result = await loader().ConfigureAwait(false);
        return new Page<ActorSummary>(result.Actors, result.NextCursor);
    }
}