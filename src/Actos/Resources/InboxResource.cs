using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Notification inbox for the authenticated actor: list notifications, mark a single one read,
/// or mark all read at once.
/// </summary>
/// <remarks>
/// <para>
/// There is <b>no push or server-sent-events</b> for notifications — a client that needs to stay
/// current must <c>watch()</c>, which means <b>polling</b> this list on an interval and respecting
/// the inbox rate-limit bucket (see the <c>X-RateLimit-*</c> response headers, 429 <c>Retry-After</c>).
/// The polling watcher is intentionally not exposed in this phase; only { list, read, readAll } are.
/// </para>
/// </remarks>
public sealed class InboxResource
{
    private readonly Actos.Transport.Transport _transport;

    internal InboxResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Lists a page of notifications for the authenticated actor.
    /// </summary>
    /// <remarks>
    /// The <paramref name="unread"/> flag filters the page to unread notifications.
    /// <b>Important:</b> <see cref="Actos.Models.InboxResponse.UnreadCount"/> is the actor's
    /// <b>total</b> unread notification count server-wide, <i>not</i> the size of this page — use it
    /// for badges/summary counts, never to size the returned <c>Notifications</c> collection.
    /// </remarks>
    /// <param name="unread">When <see langword="true"/> only unread notifications are returned.</param>
    /// <param name="limit">Maximum page size (capped by the server).</param>
    /// <param name="cursor">Opaque pagination cursor; <see langword="null"/> for the first page.</param>
    public Task<Actos.Models.InboxResponse> ListAsync(
        bool? unread = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<Actos.Models.InboxResponse>(
            HttpMethod.Get,
            "/me/inbox",
            QueryParams.Build(
                ("unread", unread is { } value ? value.ToString().ToLowerInvariant() : null),
                ("limit", limit),
                ("cursor", cursor)),
            cancellationToken: cancellationToken);

    /// <summary>Marks a single notification as read.</summary>
    /// <param name="id">Notification id.</param>
    public Task ReadAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Patch,
            $"/me/inbox/{id}/read",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Marks all notifications up to <paramref name="cursor"/> as read, or every notification when
    /// <paramref name="cursor"/> is <see langword="null"/> / empty.
    /// </summary>
    /// <param name="cursor">
    /// Marks everything up to this cursor as read. <see langword="null"/> (or empty) marks the
    /// entire inbox read.
    /// </param>
    public Task<Actos.Models.MarkAllReadResponse> ReadAllAsync(
        string? cursor = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<Actos.Models.MarkAllReadResponse>(
            HttpMethod.Post,
            "/me/inbox/read",
            QueryParams.Build(("cursor", cursor)),
            cancellationToken: cancellationToken);
}