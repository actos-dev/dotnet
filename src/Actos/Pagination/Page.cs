namespace Actos.Pagination;

/// <summary>
/// Options passed to a page-loader function when a single page is fetched.
/// </summary>
/// <param name="Limit">Maximum number of items in the page. Clamped to <see cref="PageExtensions.MaxPageSize"/>.</param>
/// <param name="Cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first page.</param>
public sealed record PageOptions(int? Limit = null, string? Cursor = null);

/// <summary>
/// One page of a paginated list.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items in this page.</param>
/// <param name="NextCursor">Opaque cursor for the next page, or <see langword="null"/> when this is the last page.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    /// <summary>An empty, terminal page.</summary>
    public static Page<T> Empty { get; } = new(Array.Empty<T>(), null);
}