using System.Net;
using System.Text;
using System.Text.Json;
using Actos.Utils;

namespace Actos.Tests;

/// <summary>
/// Exercises the Auth + Actors resources end-to-end against a stub handler: request paths,
/// snake_case bodies, tri-state PATCH semantics, the avatar endpoints and Page&lt;T&gt; mapping.
/// </summary>
public class ResourceAuthActorsTests
{
    private const string RegisterBody =
        "{\"actor\":{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"username\":\"alice\"},\"api_key\":\"actos_x_y\",\"recovery_codes\":[\"AAA-BBB-CCC\"]}";

    private const string UpdateProfileBody =
        "{\"actor\":{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"username\":\"alice\"}}";

    private const string ActorsPage1 =
        "{\"actors\":[{\"actor_type\":\"ai_agent\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"username\":\"agent1\"}],\"next_cursor\":\"cursor-2\"}";

    private const string ActorsPage2 =
        "{\"actors\":[{\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-2\"," +
        "\"username\":\"sarah\"}],\"next_cursor\":null}";

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
    public async Task UpdateMe_Bio_Set_Sends_Value_And_Omits_None_DisplayName()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, UpdateProfileBody));
        using var client = TestHarness.BuildClient(handler);

        await client.Actors.UpdateMeAsync(
            displayName: Patch<string>.None,
            bio: Patch<string>.Set("Hello"));

        var body = await RequestBodyOfAsync(handler);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Hello", doc.RootElement.GetProperty("bio").GetString());
        Assert.False(doc.RootElement.TryGetProperty("display_name", out _)); // None omitted entirely
        Assert.False(doc.RootElement.TryGetProperty("avatar", out _)); // avatar is not part of this call any more
    }

    [Fact]
    public async Task UpdateMe_DisplayName_Unset_Sends_Explicit_Null()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, UpdateProfileBody));
        using var client = TestHarness.BuildClient(handler);

        await client.Actors.UpdateMeAsync(displayName: Patch<string>.Unset);

        var body = await RequestBodyOfAsync(handler);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("display_name").ValueKind); // explicit null survives
        Assert.False(doc.RootElement.TryGetProperty("bio", out _));
    }

    [Fact]
    public async Task UploadAvatar_Sends_Multipart_With_Field_Named_File()
    {
        const string avatarJson = "{\"avatar_url\":\"http://example/av-new.png\"}";
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, avatarJson));
        using var client = TestHarness.BuildClient(handler);

        var result = await client.Actors.UploadAvatarAsync(
            FileUpload.FromBytes(Encoding.UTF8.GetBytes("pixels"), "avatar.png", "image/png"));

        Assert.Equal("http://example/av-new.png", result.AvatarUrl);
        Assert.Equal("/actors/me/avatar", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.StartsWith("multipart/form-data; boundary=", handler.LastRequest.Content!.Headers.ContentType!.ToString());

        var body = handler.LastRequestBody!;
        Assert.Contains("name=file", body);
        Assert.Contains("filename=avatar.png", body);
        Assert.Contains("pixels", body);
    }

    [Fact]
    public async Task DeleteAvatar_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = TestHarness.BuildClient(handler);

        await client.Actors.DeleteAvatarAsync();

        Assert.Equal("/actors/me/avatar", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
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
                              "\"id\":\"a-9\",\"username\":\"bot\"}," +
                              "\"key\":{\"id\":\"k-1\",\"label\":\"cli\"}," +
                              "\"permissions\":[{\"permission\":\"content.delete\",\"scope\":\"global\"}]}";
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, whoami));
        using var client = TestHarness.BuildClient(handler);

        var result = await client.Auth.WhoamiAsync();

        Assert.Equal("bot", result.Actor.Username);
        var permission = Assert.Single(result.Permissions);
        Assert.Equal("content.delete", permission.Permission);
        Assert.Equal("global", permission.Scope);
        Assert.Null(permission.Community);
    }
}