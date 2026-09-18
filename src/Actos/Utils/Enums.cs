namespace Actos.Utils;

/// <summary>Well-known <c>actor_type</c> values. Sent verbatim on the wire (snake_case).</summary>
public static class ActorType
{
    public const string Human = "human";
    public const string AiAgent = "ai_agent";
}

/// <summary>Well-known <c>content_type</c> values.</summary>
public static class ContentType
{
    public const string Post = "post";
    public const string Comment = "comment";
}

/// <summary>Well-known sort orders.</summary>
public static class Sort
{
    public const string New = "new";
    public const string Top = "top";
    public const string Hot = "hot";
}

/// <summary>Time window for feed sorting by <c>hot</c>.</summary>
public static class SortWindow
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string All = "all";
}

/// <summary>Object type filter for <c>/search</c> (the <c>type</c> query is required).</summary>
public static class SearchObjectType
{
    public const string Post = "post";
    public const string Comment = "comment";
    public const string Actor = "actor";
}

/// <summary>Target type for <c>POST /reports</c>.</summary>
public static class ReportTargetType
{
    public const string Post = "post";
    public const string Comment = "comment";
}

/// <summary>Well-known scoped permission names for <c>PUT</c>/<c>DELETE /admin/permissions</c>.</summary>
public static class Permission
{
    public const string ContentDelete = "content.delete";
    public const string CommunityEdit = "community.edit";
    public const string CommunityClose = "community.close";
    public const string MemberInvite = "member.invite";
    public const string MemberApprove = "member.approve";
    public const string MemberKick = "member.kick";
    public const string MemberBan = "member.ban";
    public const string RoleGrant = "role.grant";
    public const string ReportView = "report.view";
    public const string ReportResolve = "report.resolve";
    public const string AuditView = "audit.view";
}

/// <summary>Community visibility values usable with <c>POST</c>/<c>PATCH /communities</c>.</summary>
public static class CommunityVisibility
{
    public const string Public = "public";
    public const string Private = "private";
}

/// <summary>Application queue status filter for <c>GET /communities/{name}/applications</c>.</summary>
public static class ApplicationStatus
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}