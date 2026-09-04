using System.Web;
using Actos.Resources;

namespace Actos.Tests;

/// <summary>
/// Exercises the Inbox resource: listing notifications (with the unread filter), marking a single
/// notification read, and the mark-all-read flow including the empty-cursor (mark everything) form.
/// The resource is driven through its real <see cref="Actos.Transport.Transport"/> via the internal
/// constructor (visible to tests through <c>InternalsVisibleTo</c>).
/// </summary>
public class ResourceInboxTests
{
    private const string InboxJson =
        "{\"notifications\":[{\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"n-1\",\"kind\":\"mention\"," +
        "\"payload\":{\"post_id\":\"c-1\"},\"target_id\":\"c-1\",\"target_type\":\"post\"," +
        "\"actor\":{\"id\":\"a-1\",\"username\":\"alice\"},\"read_at\":null}," +
        "{\"created_at\":\"2026-01-01T00:00:02Z\",\"id\":\"n-2\",\"kind\":\"follow\"," +
        "\"payload\":{},\"target_id\":\"a-2\",\"target_type\":\"actor\",\"read_at\":null}]," +
        "\"unread_count\":7,\"next_cursor\":\"inbox-cur-2\"}";

    private const string MarkAllReadJson = "{\"marked\":12}";

    private static InboxResource BuildResource(
        Actos.Transport.Transport transport)
        => new(transport);

    [Fact]
    public async Task ListAsync_Hits_Inbox_And_Maps_InboxResponse()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, InboxJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var inbox = BuildResource(transport);

        var response = await inbox.ListAsync();

        Assert.Equal("/me/inbox", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);

        // UnreadCount is the TOTAL count server-wide, not the number of items on this page.
        Assert.Equal(2, response.Notifications.Count);
        Assert.Equal(7, response.UnreadCount);
        Assert.Equal("inbox-cur-2", response.NextCursor);

        Assert.Equal("n-1", response.Notifications[0].Id);
        Assert.Equal("mention", response.Notifications[0].Kind);
        Assert.Equal("n-2", response.Notifications[1].Id);
    }

    [Fact]
    public async Task ListAsync_Passes_Unread_Flag_And_Paging_Query()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, InboxJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var inbox = BuildResource(transport);

        await inbox.ListAsync(unread: true, limit: 5, cursor: "inbox-cur-1");

        var parsed = HttpUtility.ParseQueryString(handler.LastRequest!.RequestUri!.Query);
        Assert.Equal("true", parsed["unread"]); // lowercase boolean on the wire, not "True"
        Assert.Equal("5", parsed["limit"]);
        Assert.Equal("inbox-cur-1", parsed["cursor"]);
    }

    [Fact]
    public async Task ReadAsync_Sends_Patch_To_InboxId_Read()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var inbox = BuildResource(transport);

        await inbox.ReadAsync("n-42");

        Assert.Equal("/me/inbox/n-42/read", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
    }

    [Fact]
    public async Task ReadAllAsync_Sends_Post_And_Reads_Marked()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, MarkAllReadJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var inbox = BuildResource(transport);

        var response = await inbox.ReadAllAsync(cursor: "inbox-cur-5");

        Assert.Equal("/me/inbox/read", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("cursor=inbox-cur-5", handler.LastRequest!.RequestUri!.Query);
        Assert.Equal(12, response.Marked);
    }

    [Fact]
    public async Task ReadAllAsync_Empty_Cursor_Marks_All()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, MarkAllReadJson));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var inbox = BuildResource(transport);

        var response = await inbox.ReadAllAsync();

        // No cursor on the wire => marks the entire inbox read.
        Assert.Equal("/me/inbox/read", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Empty(handler.LastRequest!.RequestUri!.Query);
        Assert.Equal(12, response.Marked);
    }
}