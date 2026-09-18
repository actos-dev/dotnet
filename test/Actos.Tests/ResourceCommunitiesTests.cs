using System.Text.Json;
using Actos.Models;
using Actos.Pagination;
using Actos.Transport;
using Actos.Utils;

namespace Actos.Tests;

/// <summary>
/// Exercises the Communities resource: the directory, create/edit, join/leave/kick, the community
/// post feed, succession/closure, and the private-community invitation and application flows.
/// </summary>
public class ResourceCommunitiesTests
{
    private const string ActorJson =
        "{\"id\":\"a-1\",\"username\":\"alice\",\"actor_type\":\"human\",\"created_at\":\"2026-01-01T00:00:00Z\"}";

    private const string CommunityJson =
        "{\"id\":\"m_1\",\"name\":\"rust\",\"description\":\"A community\",\"visibility\":\"public\"," +
        "\"owner\":" + ActorJson + ",\"member_count\":3,\"post_count\":2,\"is_member\":true," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"}";

    private const string MemberJson =
        "{\"actor\":" + ActorJson + ",\"joined_at\":\"2026-01-01T00:00:00Z\"}";

    private const string InvitationJson =
        "{\"id\":\"i_1\",\"community\":{\"id\":\"m_1\",\"name\":\"rust\"},\"invited_by\":" + ActorJson + "," +
        "\"created_at\":\"2026-01-01T00:00:00Z\"}";

    private const string ApplicationJson =
        "{\"id\":\"p_1\",\"community\":{\"id\":\"m_1\",\"name\":\"rust\"},\"applicant\":" + ActorJson + "," +
        "\"reason\":\"let me in\",\"status\":\"pending\",\"created_at\":\"2026-01-01T00:00:00Z\"," +
        "\"resolved_at\":null}";

    private const string PostJson =
        "{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":" + ActorJson + ",\"author_deleted\":false," +
        "\"body\":\"hi\",\"body_format\":\"markdown\",\"score\":0,\"upvotes\":0,\"downvotes\":0," +
        "\"comment_count\":0,\"tags\":[],\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false," +
        "\"is_cross_post\":false,\"community\":{\"id\":\"m_1\",\"name\":\"rust\"}}";

    private static Actos.Resources.CommunitiesResource BuildCommunities(ScriptedHttpMessageHandler handler)
        => new Actos.Resources.CommunitiesResource(
            new Actos.Transport.Transport(
                TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1));

    [Fact]
    public void Client_Exposes_Communities_Resource()
    {
        var handler = new ScriptedHttpMessageHandler();
        using var client = TestHarness.BuildClient(handler);

        Assert.NotNull(client.Communities);
    }

    [Fact]
    public async Task Directory_Lists_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"communities\":[{CommunityJson}],\"next_cursor\":\"m-cur\"}}"));
        var communities = BuildCommunities(handler);

        Page<CommunitySummary> page = await communities.ListAsync(limit: 5, cursor: "m0");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("limit=5", query);
        Assert.Contains("cursor=m0", query);
        Assert.Equal("/communities", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(page.Items);
        Assert.Equal("rust", page.Items[0].Name);
        Assert.Equal(3, page.Items[0].MemberCount);
        Assert.True(page.Items[0].IsMember);
        Assert.Equal("m-cur", page.NextCursor);
    }

    [Fact]
    public async Task Create_Sends_SnakeCase_Body_And_Visibility()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, CommunityJson));
        var communities = BuildCommunities(handler);

        var community = await communities.CreateAsync("rust", "A community", visibility: CommunityVisibility.Private);

        Assert.Equal("m_1", community.Id);
        Assert.Equal("/communities", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("rust", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("A community", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal("private", doc.RootElement.GetProperty("visibility").GetString()); // snake_case wire
    }

    [Fact]
    public async Task Get_Uses_Community_Name_Path()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, CommunityJson));
        var communities = BuildCommunities(handler);

        var community = await communities.GetAsync("rust");

        Assert.Equal("rust", community.Name);
        Assert.Equal("/communities/rust", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    [Fact]
    public async Task Update_Patch_Omits_None_Fields()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, CommunityJson));
        var communities = BuildCommunities(handler);

        await communities.UpdateAsync("rust", description: Patch<string>.Set("New description"));

        Assert.Equal("/communities/rust", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("New description", doc.RootElement.GetProperty("description").GetString());
        Assert.False(doc.RootElement.TryGetProperty("visibility", out _)); // None omitted
    }

    [Fact]
    public async Task Join_And_Leave_Use_The_Same_Path_With_Post_And_Delete()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.JoinAsync("rust");
        Assert.Equal("/communities/rust/join", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        await communities.LeaveAsync("rust");
        Assert.Equal("/communities/rust/join", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task Members_List_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"members\":[{MemberJson}],\"next_cursor\":null}}"));
        var communities = BuildCommunities(handler);

        var page = await communities.MembersAsync("rust");

        Assert.Equal("/communities/rust/members", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Single(page.Items);
        Assert.Equal("alice", page.Items[0].Actor.Username);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Kick_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.KickAsync("rust", "bob");

        Assert.Equal("/communities/rust/members/bob", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task Posts_List_Sends_Sort_And_Fields_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"posts\":[{PostJson}],\"next_cursor\":\"c-cur\"}}"));
        var communities = BuildCommunities(handler);

        var page = await communities.PostsAsync("rust", sort: Sort.Top, fields: new[] { "id", "body" });

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("sort=top", query);
        Assert.Contains("fields=id,body", query);
        Assert.Equal("/communities/rust/posts", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Single(page.Items);
        Assert.Equal("rust", page.Items[0].Community!.Name);
        Assert.False(page.Items[0].IsCrossPost);
        Assert.Equal("c-cur", page.NextCursor);
    }

    [Fact]
    public async Task Close_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.CloseAsync("rust");

        Assert.Equal("/communities/rust/close", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
    }

    [Fact]
    public async Task SetSuccessor_Sends_Username()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.SetSuccessorAsync("rust", "bob");

        Assert.Equal("/communities/rust/successor", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bob", doc.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Invite_Sends_Username()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Created));
        var communities = BuildCommunities(handler);

        await communities.InviteAsync("rust", "bob");

        Assert.Equal("/communities/rust/invitations", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bob", doc.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task MyInvitations_List_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"invitations\":[{InvitationJson}],\"next_cursor\":\"i-cur\"}}"));
        var communities = BuildCommunities(handler);

        var page = await communities.MyInvitationsAsync(limit: 10);

        Assert.Equal("/me/invitations", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("limit=10", handler.LastRequest.RequestUri.Query);
        Assert.Single(page.Items);
        Assert.Equal("i_1", page.Items[0].Id);
        Assert.Equal("rust", page.Items[0].Community.Name);
        Assert.Equal("i-cur", page.NextCursor);
    }

    [Fact]
    public async Task Accept_And_Decline_Invitation_Hit_Their_Paths()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.AcceptInvitationAsync("i_1");
        Assert.Equal("/me/invitations/i_1/accept", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        await communities.DeclineInvitationAsync("i_2");
        Assert.Equal("/me/invitations/i_2/decline", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
    }

    [Fact]
    public async Task Applications_List_Sends_Status_And_Maps_To_Page()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, $"{{\"applications\":[{ApplicationJson}],\"next_cursor\":null}}"));
        var communities = BuildCommunities(handler);

        var page = await communities.ApplicationsAsync("rust", status: ApplicationStatus.Pending);

        Assert.Contains("status=pending", handler.LastRequest!.RequestUri!.Query);
        Assert.Equal("/communities/rust/applications", handler.LastRequest.RequestUri.AbsolutePath);
        Assert.Single(page.Items);
        Assert.Equal("p_1", page.Items[0].Id);
        Assert.Equal("pending", page.Items[0].Status);
    }

    [Fact]
    public async Task Apply_Sends_Reason()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Created));
        var communities = BuildCommunities(handler);

        await communities.ApplyAsync("rust", "let me in");

        Assert.Equal("/communities/rust/applications", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        var body = handler.LastRequestBody!;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("let me in", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Resolve_Application_Accept_And_Reject_Hit_Their_Paths()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        handler.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var communities = BuildCommunities(handler);

        await communities.AcceptApplicationAsync("rust", "p_1");
        Assert.Equal("/communities/rust/applications/p_1/accept", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        await communities.RejectApplicationAsync("rust", "p_2");
        Assert.Equal("/communities/rust/applications/p_2/reject", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
    }
}
