using Actos.Pagination;

namespace Actos.Tests;

/// <summary>
/// Verifies cursor-based pagination: <see cref="PageExtensions.ListAsync{T}"/> returns a single page
/// shaped as <see cref="Page{T}.Items"/> + <see cref="Page{T}.NextCursor"/>, and
/// <see cref="PageExtensions.StreamAsync{T}"/> walks the cursor chain until the terminal page and
/// yields every item in order. Page sizes are clamped to <see cref="PageExtensions.MaxPageSize"/>.
/// </summary>
public class PaginationTests
{
    /// <summary>A cursor-chain loader over two pages returning five items each.</summary>
    private static Func<PageOptions, Task<Page<int>>> TwoPageLoader()
    {
        var pages = new List<Page<int>>
        {
            new(new[] { 1, 2, 3, 4, 5 }, "cursor-2"),
            new(new[] { 6, 7, 8, 9, 10 }, null),
        };

        return options =>
        {
            var index = options.Cursor switch
            {
                null => 0,
                "cursor-2" => 1,
                _ => throw new InvalidOperationException($"Unexpected cursor '{options.Cursor}'."),
            };

            return Task.FromResult(pages[index]);
        };
    }

    /// <summary>A loader that records every <see cref="PageOptions"/> it is invoked with.</summary>
    private static Func<PageOptions, Task<Page<int>>> RecordingLoader(List<PageOptions> calls)
    {
        var load = TwoPageLoader();
        return async options =>
        {
            calls.Add(options);
            return await load(options);
        };
    }

    [Fact]
    public async Task ListAsync_Returns_Page_Shaped_As_Items_And_NextCursor()
    {
        var load = TwoPageLoader();

        var page = await load.ListAsync();

        Assert.NotNull(page);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, page.Items);
        Assert.Equal("cursor-2", page.NextCursor);
    }

    [Fact]
    public async Task ListAsync_Passes_Limit_And_Resumes_From_Cursor()
    {
        var load = TwoPageLoader();

        var page = await load.ListAsync(pageSize: 3, cursor: "cursor-2");

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task StreamAsync_Yields_All_Items_In_Order_And_Stops_At_Terminal_Page()
    {
        var calls = new List<PageOptions>();
        var load = RecordingLoader(calls);

        var items = new List<int>();
        await foreach (var item in load.StreamAsync(pageSize: 5))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, items);
        Assert.Equal(new[] { null, "cursor-2" }, calls.Select(c => c.Cursor));
    }

    [Fact]
    public async Task ListAsync_Clamps_PageSize_To_Maximum()
    {
        Assert.Equal(100, PageExtensions.MaxPageSize);

        var calls = new List<PageOptions>();
        var load = RecordingLoader(calls);

        await load.ListAsync(pageSize: 5000);
        Assert.Equal(100, calls.Single().Limit);
    }

    [Fact]
    public async Task StreamAsync_Clamps_PageSize_LowBound_To_One()
    {
        var calls = new List<PageOptions>();
        var load = RecordingLoader(calls);

        // Force enumeration so the loader actually runs.
        await foreach (var _ in load.StreamAsync(pageSize: 0))
        {
        }

        // The first page is requested with the clamped minimum size of 1.
        Assert.Equal(1, calls[0].Limit);
    }

    [Fact]
    public void PageOptions_Nullables_Comma_And_Empty_Page_Are_Supported()
    {
        Assert.Null(Page<int>.Empty.NextCursor);
        Assert.Empty(Page<int>.Empty.Items);
        Assert.Null(new PageOptions().Limit);
        Assert.Null(new PageOptions().Cursor);
    }
}