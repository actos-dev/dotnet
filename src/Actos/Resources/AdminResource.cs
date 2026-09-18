using Actos.Models;
using Actos.Pagination;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Moderation and administration surface. Report review, content removal, bans and scoped
/// permission grants are restricted to moderators/admins; <see cref="GrantPermissionAsync"/> and
/// <see cref="RevokePermissionAsync"/> additionally require an admin actor (<c>role.grant</c>).
/// </summary>
public sealed class AdminResource
{
    private readonly Actos.Transport.Transport _transport;

    internal AdminResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>Lists reports in the moderation queue (moderator or admin only).</summary>
    /// <param name="status">Optional report status filter (for example <c>open</c>, <c>resolved</c>, <c>dismissed</c>).</param>
    /// <param name="limit">Optional maximum page size.</param>
    /// <param name="cursor">Opaque cursor for the next page, or <see langword="null"/> for the first.</param>
    public Task<Page<ReportSummary>> ReportsAsync(
        string? status = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ReportListResponse>(
            HttpMethod.Get,
            "/admin/reports",
            QueryParams.Build(("status", status), ("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<ReportSummary>(t.Result.Reports, t.Result.NextCursor),
                cancellationToken);

    /// <summary>Resolves (or dismisses) a report. <paramref name="status"/> is <c>resolved</c> or <c>dismissed</c>.</summary>
    public Task<ReportSummary> ResolveReportAsync(
        string id,
        string status,
        string? notes = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ReportSummary>(
            HttpMethod.Patch,
            $"/admin/reports/{id}",
            body: new UpdateReportRequest(Status: status, Notes: notes),
            cancellationToken: cancellationToken);

    /// <summary>Moderator-deletes content by id (the same soft-delete a moderator can perform directly).</summary>
    public Task DeleteContentAsync(
        string id,
        string reason,
        CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/admin/contents/{id}",
            body: new ModerateDeleteRequest(Reason: reason),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Bans an actor. <paramref name="expiresAt"/> is an ISO-8601 timestamp for a temporary ban.
    /// When <paramref name="community"/> is given the ban is scoped to that community;
    /// <paramref name="deletePosts"/> additionally queues the deletion of the actor's posts there
    /// and is only valid together with a community.
    /// </summary>
    public Task<BanSummary> BanAsync(
        string username,
        string reason,
        string? expiresAt = null,
        string? community = null,
        bool? deletePosts = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<BanSummary>(
            HttpMethod.Post,
            "/admin/bans",
            body: new CreateBanRequest(
                Reason: reason,
                Username: username,
                Community: community,
                DeletePosts: deletePosts,
                ExpiresAt: expiresAt),
            cancellationToken: cancellationToken);

    /// <summary>Lifts an active ban. No-op if no ban is active. Omit <paramref name="community"/> to remove a platform-wide ban.</summary>
    public Task UnbanAsync(
        string username,
        string? community = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/admin/bans/{username}",
            QueryParams.Build(("community", community)),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Grants a scoped permission to an actor (admin only, requires <c>role.grant</c>). Idempotent.
    /// <paramref name="community"/> scopes the grant to that community; omitted means global.
    /// </summary>
    public Task GrantPermissionAsync(
        string username,
        string permission,
        string? community = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Put,
            "/admin/permissions",
            body: new SetPermissionRequest(
                Username: username,
                Permission: permission,
                Community: community),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Revokes a scoped permission from an actor (admin only, requires <c>role.grant</c>).
    /// Idempotent; removing a permission that does not exist succeeds.
    /// </summary>
    public Task RevokePermissionAsync(
        string username,
        string permission,
        string? community = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            "/admin/permissions",
            body: new SetPermissionRequest(
                Username: username,
                Permission: permission,
                Community: community),
            cancellationToken: cancellationToken);

    /// <summary>Lists the moderator/admin action log (audit log, one page).</summary>
    public Task<Page<AdminActionSummary>> ActionsAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<AdminActionListResponse>(
            HttpMethod.Get,
            "/admin/actions",
            QueryParams.Build(("limit", limit), ("cursor", cursor)),
            cancellationToken: cancellationToken)
            .ContinueWith(
                t => new Page<AdminActionSummary>(t.Result.Actions, t.Result.NextCursor),
                cancellationToken);
}