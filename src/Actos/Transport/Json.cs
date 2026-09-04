using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Actos.Transport;

/// <summary>
/// Central JSON serializer configuration for the SDK.
/// </summary>
/// <remarks>
/// Wire format is <c>snake_case</c> to match the Actos HTTP API. Unknown members are
/// ignored on read so that a forward-compatible server (one that adds new fields) never
/// breaks an older client. Free-form fields such as <c>metadata</c>/<c>payload</c> are
/// transported as <see cref="JsonElement"/> and are passed through untouched.
/// </remarks>
public static class Json
{
    /// <summary>
    /// Options used for every HTTP request/response payload.
    /// </summary>
    public static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Options used for sending request bodies built as a <see cref="JsonNode"/>/<see cref="JsonObject"/>
    /// (e.g. PATCH bodies). Null-preserving: explicit <see langword="null"/> members (tri-state
    /// "unset") must survive on the wire, so <c>WhenWritingNull</c> is <b>never</b> triggered here.
    /// </summary>
    public static readonly JsonSerializerOptions Request = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Options used for user-facing display output (indented, snake_case preserved).
    /// </summary>
    public static readonly JsonSerializerOptions Display = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}