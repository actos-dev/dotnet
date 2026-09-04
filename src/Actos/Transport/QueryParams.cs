namespace Actos.Transport;

/// <summary>
/// Builds the pre-encoded <c>query</c> dictionary consumed by
/// <see cref="Transport.RequestAsync{TResponse}"/>. <see langword="null"/> values are omitted
/// (so an optional query parameter left unset simply does not appear on the URL).
/// </summary>
public static class QueryParams
{
    /// <summary>
    /// Builds a query dictionary from name/value pairs, dropping <see langword="null"/> values.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Build(params (string Name, object? Value)[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            if (value is not null)
            {
                result[name] = value.ToString()!;
            }
        }

        return result;
    }

    /// <summary>
    /// Serializes a server-side field-selection list (<c>?fields=a,b,c</c>), or <see langword="null"/>
    /// when the list is empty or <see langword="null"/>.
    /// </summary>
    public static string? Fields(IReadOnlyCollection<string>? fields)
        => fields is { Count: > 0 } ? string.Join(',', fields) : null;
}