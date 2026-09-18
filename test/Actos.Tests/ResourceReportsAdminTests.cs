using System.Text.Json;
using Actos.Pagination;
using Actos.Transport;

namespace Actos.Tests;

/// <summary>
/// Exercises the Reports and Admin resources: report submission, the moderator report queue,
/// report resolution, content removal, (community-scoped) banning, scoped permission grant/revoke
/// and the admin audit log.
/// </summary>
public class ResourceReportsAdminTests
{
    private const string ReportJson =
        "{\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"r-1\",\"reason\":\"spam\"," +
        "\"status\":\"open\",\"target_id\":\"c-10\",\"target_type\":\"post\",\"notes\":null}";

    private const string BanJson =
        "{\"banned_at\":\"2026-01-01T00:00:00Z\",\"reason\":\"spam\",\"username\":\"bob\",\"expires_at\":null}";

    private const string AdminActionJson =
        "{\"action_type\":\"ban\",\"admin_username\":\"alice\",\"created_at\":\"2026-01-01T00:00:00Z\"," +
        "\"id\":\"x-1\",\"target_id\":5,\"target_type\":\"user\",\"reason\":\"spam\"}";

    private static Actos.Resources.ReportsResource BuildReports(ScriptedHttpMessageHandler handler)
        => new Actos.Resources.ReportsResource(
            new Actos.Transport.Transport(
                TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1));

    private static Actos.Resources.AdminResource BuildAdmin(ScriptedHttpMessageHandler handler)
        => new Actos.Resources.AdminResource(
            new Actos.Transport.Transport(
                TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1));

    [Fact]
    public async Task Report_Sends_SnakeCase_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, ReportJson));
        var reports = BuildReports(handler);

        var report = await reports.ReportAsync("post", "c-10", "spam");

        Assert.Equal("r-1", report.Id);
        Assert.Equal("/reports", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("spam", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal("c-10", doc.RootElement.GetProperty("target_id").GetString());
        Assert.Equal("post", doc.RootElement.GetProperty("target_type").GetString());
    }

    [Fact]
    public async Task AdminReports_Sends_Filters_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"reports\":[{ReportJson}],\"next_cursor\":\"r-cur\"}}"));
        var admin = BuildAdmin(handler);

        Page<Actos.Models.ReportSummary> page = await admin.ReportsAsync(
            status: "open", limit: 5, cursor: "r0");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("status=open", query);
        Assert.Contains("limit=5", query);
        Assert.Contains("cursor=r0", query);
        Assert.Equal("/admin/reports", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(page.Items);
        Assert.Equal("r-1", page.Items[0].Id);
        Assert.Equal("r-cur", page.NextCursor);
    }

    [Fact]
    public async Task ResolveReport_Patches_Status_And_Notes()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, ReportJson));
        var admin = BuildAdmin(handler);

        var report = await admin.ResolveReportAsync("r-1", "resolved", notes: "handled");

        Assert.Equal("/admin/reports/r-1", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Equal("r-1", report.Id);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("resolved", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("handled", doc.RootElement.GetProperty("notes").GetString()); // snake_case wire
    }

    [Fact]
    public async Task DeleteContent_Sends_Reason_And_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.DeleteContentAsync("c-10", "spam");

        Assert.Equal("/admin/contents/c-10", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("spam", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Ban_Sends_Username_Reason_And_ExpiresAt()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, BanJson));
        var admin = BuildAdmin(handler);

        var ban = await admin.BanAsync("bob", "spam", expiresAt: "2026-02-01T00:00:00Z");

        Assert.Equal("bob", ban.Username);
        Assert.Equal("/admin/bans", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bob", doc.RootElement.GetProperty("username").GetString());
        Assert.Equal("spam", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal("2026-02-01T00:00:00Z", doc.RootElement.GetProperty("expires_at").GetString()); // snake_case wire
        Assert.False(doc.RootElement.TryGetProperty("community", out _)); // omitted when global
        Assert.False(doc.RootElement.TryGetProperty("delete_posts", out _));
    }

    [Fact]
    public async Task Ban_With_Community_And_DeletePosts_Sends_Scoped_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, BanJson));
        var admin = BuildAdmin(handler);

        await admin.BanAsync("bob", "spam", community: "rust", deletePosts: true);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("rust", doc.RootElement.GetProperty("community").GetString());
        Assert.True(doc.RootElement.GetProperty("delete_posts").GetBoolean());
    }

    [Fact]
    public async Task Unban_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.UnbanAsync("bob");

        Assert.Equal("/admin/bans/bob", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
        Assert.DoesNotContain("community=", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task Unban_With_Community_Appends_Query()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.UnbanAsync("bob", community: "rust");

        Assert.Contains("community=rust", handler.LastRequest!.RequestUri!.Query);
        Assert.Equal("/admin/bans/bob", handler.LastRequest.RequestUri.AbsolutePath);
    }

    [Fact]
    public async Task GrantPermission_Puts_Global_Grant()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.GrantPermissionAsync("bob", "role.grant");

        Assert.Equal("/admin/permissions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bob", doc.RootElement.GetProperty("username").GetString());
        Assert.Equal("role.grant", doc.RootElement.GetProperty("permission").GetString());
        Assert.False(doc.RootElement.TryGetProperty("community", out _)); // omitted for a global grant
    }

    [Fact]
    public async Task GrantPermission_With_Community_Scopes_Grant()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.GrantPermissionAsync("bob", "member.kick", community: "rust");

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("member.kick", doc.RootElement.GetProperty("permission").GetString());
        Assert.Equal("rust", doc.RootElement.GetProperty("community").GetString());
    }

    [Fact]
    public async Task RevokePermission_Deletes_With_Body()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var admin = BuildAdmin(handler);

        await admin.RevokePermissionAsync("bob", "member.kick", community: "rust");

        Assert.Equal("/admin/permissions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bob", doc.RootElement.GetProperty("username").GetString());
        Assert.Equal("member.kick", doc.RootElement.GetProperty("permission").GetString());
        Assert.Equal("rust", doc.RootElement.GetProperty("community").GetString());
    }

    [Fact]
    public async Task Actions_Lists_Audit_Log_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"actions\":[{AdminActionJson}],\"next_cursor\":\"x-cur\"}}"));
        var admin = BuildAdmin(handler);

        var page = await admin.ActionsAsync(limit: 10, cursor: "x0");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("limit=10", query);
        Assert.Contains("cursor=x0", query);
        Assert.Equal("/admin/actions", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(page.Items);
        Assert.Equal("x-1", page.Items[0].Id);
        Assert.Equal("ban", page.Items[0].ActionType);
        Assert.Equal("x-cur", page.NextCursor);
    }
}