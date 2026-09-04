using Actos.Models;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Reports content to moderators. Any authenticated actor may report a post, comment or actor;
/// the generated report enters the moderator/admin moderation queue.
/// </summary>
public sealed class ReportsResource
{
    private readonly Actos.Transport.Transport _transport;

    internal ReportsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>Reports a piece of content for review. Any authenticated actor.</summary>
    /// <param name="targetType">The kind of target being reported (for example <c>post</c>, <c>comment</c> or <c>actor</c>).</param>
    /// <param name="targetId">The id of the reported target.</param>
    /// <param name="reason">Human-readable reason for the report.</param>
    public Task<ReportSummary> ReportAsync(
        string targetType,
        string targetId,
        string reason,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ReportSummary>(
            HttpMethod.Post,
            "/reports",
            body: new CreateReportRequest(
                Reason: reason,
                TargetId: targetId,
                TargetType: targetType),
            cancellationToken: cancellationToken);
}