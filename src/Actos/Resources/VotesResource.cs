using Actos.Models;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Content voting: cast or clear a tri-state vote (-1 / 0 / +1) on a single content item, and
/// read back the caller's current vote state for a batched set of content ids.
/// </summary>
public sealed class VotesResource
{
    private readonly Actos.Transport.Transport _transport;

    internal VotesResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Sets the caller's vote on a content item. <paramref name="value"/> is tri-state:
    /// <c>-1</c> (downvote), <c>0</c> (clear/neutral), <c>1</c> (upvote). Returns the post-vote
    /// score state.
    /// </summary>
    /// <param name="id">The content id (<c>c_...</c>) to vote on.</param>
    /// <param name="value">The vote value: <c>-1</c>, <c>0</c> or <c>1</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<VoteResponse> VoteAsync(
        string id,
        int value,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<VoteResponse>(
            HttpMethod.Put,
            $"/contents/{id}/vote",
            body: new VoteRequest(Value: value),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Fetches the caller's current vote for each of the given content ids as a map of
    /// <c>content id → vote (-1 / 0 / 1)</c>. Ids are sent comma-joined as
    /// <c>?content_ids=a,b,c</c>; at most 100 ids are sent (additional ids are ignored server-side).
    /// </summary>
    /// <param name="contentIds">The content ids to look up (at most 100 are sent).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<IReadOnlyDictionary<string, int>> MyVotesAsync(
        IEnumerable<string> contentIds,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<VoteMapResponse>(
            HttpMethod.Get,
            "/me/votes",
            QueryParams.Build(("content_ids", string.Join(',', contentIds.Take(100)))),
            cancellationToken: cancellationToken)
            .ContinueWith(t => (IReadOnlyDictionary<string, int>)t.Result.Votes, cancellationToken);
}