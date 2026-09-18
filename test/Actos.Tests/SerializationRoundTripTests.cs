using System.Text.Json;
using Actos.Models;
using Actos.Transport;

namespace Actos.Tests;

/// <summary>
/// Verifies that the wire serialization contract holds: snake_case keys on the wire,
/// lossless round-tripping, and untrouched pass-through of free-form JSON fields.
/// </summary>
public class SerializationRoundTripTests
{
    [Fact]
    public void ActorSummary_Serializes_WithSnakeCaseWireKeys()
    {
        var actor = TestHarness.SampleActor();

        var json = JsonSerializer.Serialize(actor, Json.Wire);

        Assert.Contains("\"actor_type\"", json);
        Assert.Contains("\"avatar_url\"", json);
        Assert.Contains("\"display_name\"", json);
        Assert.Contains("\"created_at\"", json);
    }

    [Fact]
    public void ActorSummary_DeserializesBack_ToEqualValues()
    {
        var actor = TestHarness.SampleActor();

        var json = JsonSerializer.Serialize(actor, Json.Wire);
        var roundTripped = JsonSerializer.Deserialize<ActorSummary>(json, Json.Wire);

        Assert.NotNull(roundTripped);
        Assert.Equal(actor, roundTripped);
        Assert.Equal("alice", roundTripped.Username);
        Assert.Equal("http://example/av.png", roundTripped.AvatarUrl);
    }

    [Fact]
    public void ContentSummary_RoundTrips_NullVsEmpty_Attachments()
    {
        // None ("attachments were not loaded") and Some([]) ("no attachments") are distinct —
        // collapsing them would lose information the server deliberately keeps apart.
        var withoutAttachments = new ContentSummary(
            Author: TestHarness.SampleActor(),
            AuthorDeleted: false,
            Body: "Hello world",
            BodyFormat: "markdown",
            CommentCount: 0,
            ContentType: "note",
            CreatedAt: "2026-01-01T00:00:00Z",
            Deleted: false,
            Downvotes: 0,
            Id: "c-1",
            IsCrossPost: false,
            Score: 5,
            Tags: new[] { "a", "b" },
            Upvotes: 5,
            Attachments: null);

        var withEmptyAttachments = withoutAttachments with { Attachments = Array.Empty<UploadResponse>() };

        var roundTrippedNull = JsonSerializer.Deserialize<ContentSummary>(
            JsonSerializer.Serialize(withoutAttachments, Json.Wire), Json.Wire);
        var roundTrippedEmpty = JsonSerializer.Deserialize<ContentSummary>(
            JsonSerializer.Serialize(withEmptyAttachments, Json.Wire), Json.Wire);

        Assert.NotNull(roundTrippedNull);
        Assert.NotNull(roundTrippedEmpty);
        Assert.Null(roundTrippedNull.Attachments);
        Assert.NotNull(roundTrippedEmpty.Attachments);
        Assert.Empty(roundTrippedEmpty.Attachments!);
    }

    [Fact]
    public void ContentSummary_CrossPost_Tombstone_Keeps_IsCrossPost_True_And_CrossPost_Null()
    {
        // is_cross_post=true with a null cross_post is the "unreachable source" tombstone: the
        // source was deleted or is invisible to this reader, and the two are deliberately
        // undifferentiated. The flag must survive while the preview stays null.
        const string json =
            "{\"id\":\"c-1\",\"content_type\":\"post\",\"author\":" + TestHarness.ActorJson + "," +
            "\"author_deleted\":false,\"body\":\"\",\"body_format\":\"markdown\",\"score\":0," +
            "\"upvotes\":0,\"downvotes\":0,\"comment_count\":0,\"tags\":[]," +
            "\"created_at\":\"2026-01-01T00:00:00Z\",\"deleted\":false,\"is_cross_post\":true," +
            "\"cross_post\":null}";

        var content = JsonSerializer.Deserialize<ContentSummary>(json, Json.Wire);

        Assert.NotNull(content);
        Assert.True(content.IsCrossPost);
        Assert.Null(content.CrossPost);
        Assert.Null(content.Community);
    }

    [Fact]
    public void Wire_Reads_SnakeCase_With_CaseInsensitivity()
    {
        // The wire options are case-insensitive on read; mixed-case keys still bind.
        const string mixedCase = "{\"Actor_Type\":\"human\",\"Created_At\":\"2026-01-01T00:00:00Z\"," +
            "\"ID\":\"a-9\",\"USERNAME\":\"bob\"}";

        var actor = JsonSerializer.Deserialize<ActorSummary>(mixedCase, Json.Wire);

        Assert.NotNull(actor);
        Assert.Equal("human", actor.ActorType);
        Assert.Equal("bob", actor.Username);
    }
}