using System.Text.Json;
using System.Text.Json.Nodes;
using Actos.Utils;

namespace Actos.Transport;

/// <summary>
/// Builds request bodies for PATCH (and other partial-update) calls. Fields are added
/// explicitly so an omitted field is genuinely not sent, and tri-state <see cref="Patch{T}"/>
/// values map to omit / concrete value / explicit null as appropriate.
/// </summary>
public static class RequestBody
{
    /// <summary>Creates an empty request body object.</summary>
    public static JsonObject New() => new();

    /// <summary>
    /// Adds <paramref name="value"/> under <paramref name="name"/> (the snake_case wire name). When
    /// <paramref name="value"/> is <see langword="null"/> and <paramref name="omitWhenNull"/> is true,
    /// the field is left out entirely.
    /// </summary>
    public static JsonObject Set(this JsonObject body, string name, object? value, bool omitWhenNull = true)
    {
        if (value is null && omitWhenNull)
        {
            return body;
        }

        body[name] = value is null ? null : JsonSerializer.SerializeToNode(value, Json.Wire);
        return body;
    }

    /// <summary>
    /// Applies a tri-state <see cref="Patch{T}"/>. <see cref="Patch{T}.None"/> omits the field,
    /// <see cref="Patch{T}.Set"/> sends the concrete value, and <see cref="Patch{T}.Unset"/> sends an
    /// explicit <see langword="null"/> (to clear the server value).
    /// </summary>
    public static JsonObject Set<T>(this JsonObject body, string name, Patch<T> patch)
    {
        if (patch.IsNone)
        {
            return body;
        }

        body[name] = patch.IsUnset ? null : JsonSerializer.SerializeToNode(patch.Value, Json.Wire);
        return body;
    }
}