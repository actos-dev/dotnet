using Actos.Models;
using Actos.Pagination;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Moderation and administration surface. Report review, content removal, user bans and role
/// assignment are restricted to moderators/admins; <see cref="SetRoleAsync"/> additionally
/// requires an admin actor.
/// </summary>
/// <remarks>
/// Role assignment intentionally sends an explicit JSON <c>null</c> for the <c>role</c> field when
/// the role argument is <see langword="null"/>: the typed <see cref="SetRoleRequest"/> DTO is
/// serialized with the wire profile, which omits null members, so a typed body could not express
/// "remove the role". A <see cref="RequestBody"/> (a <see cref="System.Text.Json.Nodes.JsonObject"/>)
/// body is instead serialized with the null-preserving request profile, so <c>{"role": null}</c>
/// reaches the server and "role null removes" holds.
/// </remarks>
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

    /// <summary>Bans an actor. <paramref name="expiresAt"/> is an ISO-8601 timestamp for a temporary ban.</summary>
    public Task<BanSummary> BanAsync(
        string username,
        string reason,
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<BanSummary>(
            HttpMethod.Post,
            "/admin/bans",
            body: new CreateBanRequest(
                Reason: reason,
                Username: username,
                ExpiresAt: expiresAt),
            cancellationToken: cancellationToken);

    /// <summary>Lifts an active ban. No-op if no ban is active.</summary>
    public Task UnbanAsync(string username, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/admin/bans/{username}",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Assigns (or, when <paramref name="role"/> is <see langword="null"/>, removes) an actor's role.
    /// Admin only. A null role is sent as an explicit JSON <c>null</c> so the server can distinguish
    /// "clear the role" from "unset" — see the class remarks.
    /// </summary>
    public Task SetRoleAsync(
        string username,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var body = RequestBody.New()
            .Set("username", username)
            .Set("role", role, omitWhenNull: false);
        return _transport.RequestNoContentAsync(
            HttpMethod.Post,
            "/admin/roles",
            body: body,
            cancellationToken: cancellationToken);
    }

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