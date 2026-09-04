using Actos.Pagination;
using Actos.Utils;

namespace Actos.Tests;

/// <summary>
/// Exercises the Feed, Search and Tags resources: page mapping from their typed response models,
/// the required/optional query parameters each endpoint sends, and the cursor propagation into
/// <see cref="Page{T}.NextCursor"/>.
/// </summary>
public class ResourceFeedSearchTagsTests
{
    private const string ContentJson =
        "{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":{\"id\":\"a-1\",\"username\":\"alice\"," +
        "\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"trust_level\":0}," +
        "\"author_deleted\":false,\"body\":\"hi\",\"body_format\":\"markdown\",\"metadata\":{}," +
        "\"score\":0,\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false}";

    private const string FeedJson =
        "{\"posts\":[" + ContentJson + "],\"next_cursor\":\"f-cur\"}";

    private const string SearchJson =
        "{\"results\":[" + ContentJson + "],\"next_cursor\":\"s-cur\"}";

    private const string TagsJson =
        "{\"tags\":[{\"name\":\"net\",\"post_count\":12,\"created_at\":\"2026-01-01T00:00:00Z\"}],\"next_cursor\":\"t-cur\"}";

    private const string TagsSearchJson =
        "{\"tags\":[{\"name\":\"net\"},{\"name\":\"nest\"}]}";

    [Fact]
    public async Task FeedList_Maps_Page_And_Sends_All_Query_Params()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, FeedJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var feed = new Actos.Resources.FeedResource(transport);

        var page = await feed.ListAsync(
            sort: Utils.Sort.New,
            window: Utils.SortWindow.All,
            actorType: Utils.ActorType.AiAgent);

        Assert.Equal("/feed", handler.LastRequest!.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("sort=new", query);
        Assert.Contains("window=all", query);
        Assert.Contains("actor_type=ai_agent", query);
        Assert.Single(page.Items);
        Assert.Equal("c-1", page.Items[0].Id);
        Assert.Equal("f-cur", page.NextCursor);
    }

    [Fact]
    public async Task FeedFollowing_Hits_Following_Path()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, FeedJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var feed = new Actos.Resources.FeedResource(transport);

        var page = await feed.FollowingAsync(sort: Utils.Sort.Hot, window: Utils.SortWindow.Day);

        Assert.Equal("/feed/following", handler.LastRequest!.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("sort=hot", query);
        Assert.Contains("window=day", query);
        Assert.Single(page.Items);
        Assert.Equal("f-cur", page.NextCursor);
    }

    [Fact]
    public async Task FeedStream_Walks_Cursor_Chain()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, FeedJson));
        handler.Enqueue(TestHarness.JsonResponse(200, "{ \"posts\": [], \"next_cursor\": null }"));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var feed = new Actos.Resources.FeedResource(transport);

        var items = new List<string>();
        await foreach (var item in feed.StreamAsync(sort: Utils.Sort.New))
        {
            items.Add(item.Id);
        }

        Assert.Equal(2, handler.RequestCount);
        Assert.Single(items);
        Assert.Equal("c-1", items[0]);
    }

    [Fact]
    public async Task Search_Content_Passes_Type_And_Maps_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, SearchJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var search = new Actos.Resources.SearchResource(transport);

        var page = await search.ContentSearchAsync("hello", type: Utils.SearchObjectType.Comment);

        Assert.Equal("/search", handler.LastRequest!.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("q=hello", query);
        Assert.Contains("type=comment", query);
        Assert.Single(page.Items);
        Assert.Equal("c-1", page.Items[0].Id);
        Assert.Equal("s-cur", page.NextCursor);
    }

    [Fact]
    public async Task TagsSearch_Hits_Search_Path_With_Q()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, TagsSearchJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var tags = new Actos.Resources.TagsResource(transport);

        var result = await tags.SearchTagsAsync("net");

        Assert.Equal("/tags/search", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("q=net", handler.LastRequest.RequestUri.Query);
        Assert.Equal(2, result.Tags.Count);
        Assert.Equal("nest", result.Tags[1].Name);
    }

    [Fact]
    public async Task TagsPosts_Maps_Page_And_Sends_Sort()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, FeedJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var tags = new Actos.Resources.TagsResource(transport);

        var page = await tags.PostsAsync("net", sort: Utils.Sort.Top);

        Assert.Equal("/tags/net/posts", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("sort=top", handler.LastRequest.RequestUri.Query);
        Assert.Single(page.Items);
        Assert.Equal("c-1", page.Items[0].Id);
        Assert.Equal("f-cur", page.NextCursor);
    }

    [Fact]
    public async Task TagsList_Maps_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, TagsJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var tags = new Actos.Resources.TagsResource(transport);

        var page = await tags.ListAsync(limit: 10);

        Assert.Equal("/tags", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("limit=10", handler.LastRequest.RequestUri.Query);
        Assert.Single(page.Items);
        Assert.Equal("net", page.Items[0].Name);
        Assert.Equal(12, page.Items[0].PostCount);
        Assert.Equal("t-cur", page.NextCursor);
    }
}