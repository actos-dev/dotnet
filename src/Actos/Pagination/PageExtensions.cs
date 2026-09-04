using System.Runtime.CompilerServices;

namespace Actos.Pagination;

/// <summary>
/// Pagination helpers. <see cref="StreamAsync{T}"/> walks the cursor chain transparently and
/// yields every item across all pages as a single <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public static class PageExtensions
{
    /// <summary>The maximum number of items a single page may request.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The default page size used when none is specified.</summary>
    public const int DefaultPageSize = 100;

    private static int Clamp(int pageSize)
        => Math.Clamp(pageSize, 1, MaxPageSize);

    /// <summary>
    /// Fetches one page of items from a paginated endpoint.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="load">A function that loads a single page given <see cref="PageOptions"/>.</param>
    /// <param name="pageSize">Maximum items per page (clamped to <see cref="MaxPageSize"/>).</param>
    /// <param name="cursor">Opaque cursor for the page to fetch, or <see langword="null"/> for the first.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public static Task<Page<T>> ListAsync<T>(
        this Func<PageOptions, Task<Page<T>>> load,
        int? pageSize = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        int? limit = pageSize.HasValue ? Clamp(pageSize.Value) : null;
        return load(new PageOptions(limit, cursor));
    }

    /// <summary>
    /// Streams every item across all pages, following <see cref="Page{T}.NextCursor"/> until the last page.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="load">A function that loads a single page given <see cref="PageOptions"/>.</param>
    /// <param name="pageSize">Maximum items per page (clamped to <see cref="MaxPageSize"/>).</param>
    /// <param name="cancellationToken">A cancellation token honored between pages.</param>
    public static async IAsyncEnumerable<T> StreamAsync<T>(
        this Func<PageOptions, Task<Page<T>>> load,
        int pageSize = DefaultPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int limit = Clamp(pageSize);
        string? cursor = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await load(new PageOptions(limit, cursor)).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                yield return item;
            }

            cursor = page.NextCursor;
        }
        while (!string.IsNullOrEmpty(cursor));
    }
}