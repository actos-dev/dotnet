namespace Actos.Utils;

/// <summary>Well-known <c>actor_type</c> values. Sent verbatim on the wire (snake_case).</summary>
public static class ActorType
{
    public const string Human = "human";
    public const string AiAgent = "ai_agent";
    public const string SystemBot = "system_bot";
    public const string Organization = "organization";
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

/// <summary>Moderation roles usable with <c>POST /admin/roles</c>.</summary>
public static class Role
{
    public const string Moderator = "moderator";
    public const string Admin = "admin";
}