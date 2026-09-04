using System.Net;
using System.Text.Json;
using Actos.Utils;

namespace Actos.Tests;

/// <summary>
/// Exercises the Auth + Actors resources end-to-end against a stub handler: request paths,
/// snake_case bodies, tri-state PATCH semantics and Page&lt;T&gt; mapping.
/// </summary>
public class ResourceAuthActorsTests
{
    private const string RegisterBody =
        "{\"actor\":{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"trust_level\":0,\"username\":\"alice\"},\"api_key\":\"actos_x_y\",\"recovery_codes\":[\"AAA-BBB-CCC\"]}";

    private const string UpdateProfileBody =
        "{\"actor\":{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"trust_level\":0,\"username\":\"alice\"}}";

    private const string ActorsPage1 =
        "{\"actors\":[{\"actor_type\":\"ai_agent\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"trust_level\":0,\"username\":\"agent1\"}],\"next_cursor\":\"cursor-2\"}";

    private const string ActorsPage2 =
        "{\"actors\":[{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-2\"," +
        "\"trust_level\":1,\"username\":\"sarah\"}],\"next_cursor\":null}";

    private static Task<string> RequestBodyOfAsync(ScriptedHttpMessageHandler handler)
        => Task.FromResult(handler.LastRequestBody!);

    [Fact]
    public async Task Register_Sends_SnakeCase_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, RegisterBody));
        using var client = TestHarness.BuildClient(handler);

        var result = await client.Auth.RegisterAsync("alice", ActorType.Human, displayName: "Ali");

        Assert.Equal("a-1", result.Actor.Id);
        Assert.Equal("actos_x_y", result.ApiKey);
        Assert.Single(result.RecoveryCodes);
        Assert.Equal("/auth/register", handler.LastRequest!.RequestUri!.AbsolutePath);

        var body = await RequestBodyOfAsync(handler);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("human", doc.RootElement.GetProperty("actor_type").GetString()); // snake_case, not camelCase
        Assert.Equal("alice", doc.RootElement.GetProperty("username").GetString());
        Assert.False(doc.RootElement.TryGetProperty("displayName", out _)); // not camelCase
    }

    [Fact]
    public async Task UpdateMe_Unset_Avatar_Sends_Explicit_Null_And_Omits_None()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, UpdateProfileBody));
        using var client = TestHarness.BuildClient(handler);

        await client.Actors.UpdateMeAsync(
            displayName: Patch<string>.None,
            bio: Patch<string>.Set("Hello"),
            avatar: Patch<string>.Unset);

        var body = await RequestBodyOfAsync(handler);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Hello", doc.RootElement.GetProperty("bio").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("avatar").ValueKind); // explicit null survives
        Assert.False(doc.RootElement.TryGetProperty("display_name", out _)); // None omitted entirely
    }

    [Fact]
    public async Task UpdateMe_Set_Clears_Unused_Fields()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, UpdateProfileBody));
        using var client = TestHarness.BuildClient(handler);

        await client.Actors.UpdateMeAsync(avatar: Patch<string>.Set("f-99"));

        var body = await RequestBodyOfAsync(handler);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("f-99", doc.RootElement.GetProperty("avatar").GetString());
    }

    [Fact]
    public async Task ActorList_Maps_To_Page_With_NextCursor()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ActorsPage1));
        using var client = TestHarness.BuildClient(handler);

        var page = await client.Actors.ListAsync();

        Assert.Single(page.Items);
        Assert.Equal("agent1", page.Items[0].Username);
        Assert.Equal("cursor-2", page.NextCursor);
    }

    [Fact]
    public async Task StreamAsync_Follows_Cursor_Chain()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ActorsPage1));
        handler.Enqueue(TestHarness.JsonResponse(200, ActorsPage2));
        using var client = TestHarness.BuildClient(handler);

        var names = new List<string>();
        await foreach (var actor in client.Actors.StreamAsync())
        {
            names.Add(actor.Username);
        }

        Assert.Equal(new[] { "agent1", "sarah" }, names);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task RevokeKey_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = TestHarness.BuildClient(handler);

        await client.Auth.RevokeKeyAsync("key_123");

        Assert.Equal("/auth/keys/key_123", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task Whoami_Returns_Identity()
    {
        const string whoami = "{\"actor\":{\"actor_type\":\"ai_agent\",\"created_at\":\"2026-01-01T00:00:00Z\"," +
                              "\"id\":\"a-9\",\"trust_level\":1,\"username\":\"bot\"}," +
                              "\"key\":{\"id\":\"k-1\",\"label\":\"cli\"},\"roles\":[\"moderator\"]}";
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, whoami));
        using var client = TestHarness.BuildClient(handler);

        var result = await client.Auth.WhoamiAsync();

        Assert.Equal("bot", result.Actor.Username);
        Assert.Single(result.Roles);
    }
}