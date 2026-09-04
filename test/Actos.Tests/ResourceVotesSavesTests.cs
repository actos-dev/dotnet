using System.Text.Json;
using Actos.Models;
using Actos.Pagination;
using Actos.Resources;

namespace Actos.Tests;

/// <summary>
/// Exercises the Votes and Saves resources: tri-state PUT vote, the batched <c>content_ids</c>
/// query, the 204 save/unsave mutators and the cursor-paginated saves list.
/// </summary>
public class ResourceVotesSavesTests
{
    private const string VoteJson =
        "{\"value\":1,\"score\":7,\"upvotes\":8,\"downvotes\":1}";

    private const string VotesJson =
        "{\"votes\":{\"c-1\":1,\"c-2\":-1,\"c-5\":0}}";

    private const string ContentJson =
        "{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":{\"id\":\"a-1\",\"username\":\"alice\"," +
        "\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"trust_level\":0}," +
        "\"author_deleted\":false,\"body\":\"hi\",\"body_format\":\"markdown\",\"metadata\":{}," +
        "\"score\":0,\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false}";

    private const string SavesJson =
        "{\"saves\":[{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":{\"id\":\"a-1\",\"username\":\"alice\"," +
        "\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"trust_level\":0}," +
        "\"author_deleted\":false,\"body\":\"hi\",\"body_format\":\"markdown\",\"metadata\":{}," +
        "\"score\":0,\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false}],\"next_cursor\":\"c-cur\"}";

    private static VotesResource BuildVotes(ScriptedHttpMessageHandler handler)
    {
        var transport = new Actos.Transport.Transport(TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        return new VotesResource(transport);
    }

    private static SavesResource BuildSaves(ScriptedHttpMessageHandler handler)
    {
        var transport = new Actos.Transport.Transport(TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        return new SavesResource(transport);
    }

    [Fact]
    public async Task VoteAsync_Sends_Put_With_SnakeCase_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, VoteJson));
        var votes = BuildVotes(handler);

        var result = await votes.VoteAsync("c-1", 1);

        Assert.Equal("/contents/c-1/vote", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);
        Assert.Equal(1, result.Value);
        Assert.Equal(7, result.Score);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(1, doc.RootElement.GetProperty("value").GetInt32()); // snake_case wire
    }

    [Fact]
    public async Task VoteAsync_Can_Clear_To_Neutral_Zero()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, VoteJson));
        var votes = BuildVotes(handler);

        await votes.VoteAsync("c-1", 0);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0, doc.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task MyVotesAsync_Sends_CommaJoined_ContentIds_And_Returns_Map()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, VotesJson));
        var votes = BuildVotes(handler);

        var map = await votes.MyVotesAsync(new[] { "c-1", "c-2", "c-5" });

        Assert.Equal("/me/votes", handler.LastRequest!.RequestUri!.AbsolutePath);
        var query = handler.LastRequest.RequestUri.Query;
        Assert.Contains("content_ids=c-1,c-2,c-5", query); // comma not re-encoded
        Assert.Equal(1, map["c-1"]);
        Assert.Equal(-1, map["c-2"]);
        Assert.Equal(0, map["c-5"]);
    }

    [Fact]
    public async Task MyVotesAsync_Caps_ContentIds_At_OneHundred()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, VotesJson));
        var votes = BuildVotes(handler);

        var ids = Enumerable.Range(0, 150).Select(i => $"c-{i}").ToList();
        await votes.MyVotesAsync(ids);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("content_ids=c-0,c-1", query);
        Assert.Contains("c-98,c-99", query);
        Assert.DoesNotContain("c-100", query); // capped at the first 100 ids
    }

    [Fact]
    public async Task SaveAsync_Sends_Put_And_Returns_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var saves = BuildSaves(handler);

        await saves.SaveAsync("c-1");

        Assert.Equal("/contents/c-1/save", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);
    }

    [Fact]
    public async Task UnsaveAsync_Sends_Delete_And_Returns_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var saves = BuildSaves(handler);

        await saves.UnsaveAsync("c-1");

        Assert.Equal("/contents/c-1/save", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task SavesAsync_Maps_To_Page_With_Cursor()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, SavesJson));
        var saves = BuildSaves(handler);

        Page<ContentSummary> page = await saves.SavesAsync();

        Assert.Equal("/me/saves", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Single(page.Items);
        Assert.Equal("c-1", page.Items[0].Id);
        Assert.Equal("c-cur", page.NextCursor);
    }

    [Fact]
    public async Task SavesAsync_Appends_Optional_Query_Params()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, SavesJson));
        var saves = BuildSaves(handler);

        await saves.SavesAsync(limit: 20, cursor: "c-cur", fields: new[] { "id", "title" });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("limit=20", query);
        Assert.Contains("cursor=c-cur", query);
        Assert.Contains("fields=id,title", query);
    }
}