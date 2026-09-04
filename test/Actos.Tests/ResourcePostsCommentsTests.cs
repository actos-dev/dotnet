using System.Text.Json;

namespace Actos.Tests;

/// <summary>
/// Exercises the Posts and Comments resources: create (auto idempotency), fields query,
/// tri-state PATCH, comment nesting and the body_html flag.
/// </summary>
public class ResourcePostsCommentsTests
{
    private const string ContentJson =
        "{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":{\"id\":\"a-1\",\"username\":\"alice\"," +
        "\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"trust_level\":0}," +
        "\"author_deleted\":false,\"body\":\"hi\",\"body_format\":\"markdown\",\"metadata\":{}," +
        "\"score\":0,\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false}";

    private const string ThreadJson =
        "{\"comments\":[{\"id\":\"c-2\",\"content_type\":\"comment\",\"author\":{\"id\":\"a-1\"," +
        "\"username\":\"alice\",\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"trust_level\":0}," +
        "\"author_deleted\":false,\"body\":\"reply\",\"body_format\":\"markdown\",\"metadata\":{}," +
        "\"score\":0,\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false,\"replies\":[]}],\"next_cursor\":\"c-cur\"}";

    [Fact]
    public async Task CreatePost_Adds_Auto_IdempotencyKey_And_SnakeCase_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, ContentJson));
        using var client = TestHarness.BuildClient(handler);

        var post = await client.Posts.CreateAsync("Hello", "World", tags: new[] { "net" });

        Assert.Equal("c-1", post.Id);
        var key = handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single();
        Assert.False(string.IsNullOrWhiteSpace(key)); // auto-generated UUID, not empty

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Hello", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("net", doc.RootElement.GetProperty("tags")[0].GetString()); // snake_case tags
        Assert.False(doc.RootElement.TryGetProperty("attachmentIds", out _));
    }

    [Fact]
    public async Task GetPost_Appends_Fields_Query()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ContentJson));
        using var client = TestHarness.BuildClient(handler);

        await client.Posts.GetAsync("c-1", fields: new[] { "id", "body" });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("fields=id,body", query); // BuildUri does not re-encode the comma
    }

    [Fact]
    public async Task UpdatePost_Patch_Omits_None_Fields()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ContentJson));
        using var client = TestHarness.BuildClient(handler);

        await client.Posts.UpdateAsync("c-1", title: Utils.Patch<string>.Set("New title"));

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("New title", doc.RootElement.GetProperty("title").GetString());
        Assert.False(doc.RootElement.TryGetProperty("body", out _)); // None omitted
    }

    [Fact]
    public async Task CreateComment_Sends_ParentId()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, ContentJson));
        using var client = TestHarness.BuildClient(handler);

        await client.Comments.CreateAsync("c-1", "an answer", parentId: "c-5");

        Assert.Equal("/posts/c-1/comments", handler.LastRequest!.RequestUri!.AbsolutePath);
        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("c-5", doc.RootElement.GetProperty("parent_id").GetString()); // snake_case wire
    }

    [Fact]
    public async Task ListComments_Sends_BodyHtml_Flag_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ThreadJson));
        using var client = TestHarness.BuildClient(handler);

        var page = await client.Comments.ListAsync("c-1", bodyHtml: true);

        Assert.Contains("body_html=true", handler.LastRequest!.RequestUri!.Query);
        Assert.Single(page.Items);
        Assert.Equal("c-2", page.Items[0].Id);
        Assert.Equal("c-cur", page.NextCursor);
    }

    [Fact]
    public async Task DeletePost_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        using var client = TestHarness.BuildClient(handler);

        await client.Posts.DeleteAsync("c-1");

        Assert.Equal("/posts/c-1", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }
}