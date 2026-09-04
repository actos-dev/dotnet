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
        Assert.Contains("\"trust_level\"", json);
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
    public void ContentSummary_RoundTrips_FreeFormMetadata_AsUntouchedJsonElement()
    {
        using var metadataDocument = JsonDocument.Parse(
            "{\"x\":1,\"nested\":{\"y\":\"value\"},\"list\":[1,2,3]}");
        var summary = new ContentSummary(
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
            Metadata: metadataDocument.RootElement,
            Score: 5,
            Tags: new[] { "a", "b" },
            Upvotes: 5);

        var json = JsonSerializer.Serialize(summary, Json.Wire);

        Assert.Contains("\"metadata\"", json);

        var roundTripped = JsonSerializer.Deserialize<ContentSummary>(json, Json.Wire);

        Assert.NotNull(roundTripped);
        Assert.Equal(1, roundTripped.Metadata.GetProperty("x").GetInt32());
        Assert.Equal("value", roundTripped.Metadata.GetProperty("nested").GetProperty("y").GetString());
        Assert.Equal(3, roundTripped.Metadata.GetProperty("list").GetArrayLength());
    }

    [Fact]
    public void Wire_Reads_SnakeCase_With_CaseInsensitivity()
    {
        // The wire options are case-insensitive on read; mixed-case keys still bind.
        const string mixedCase = "{\"Actor_Type\":\"user\",\"Created_At\":\"2026-01-01T00:00:00Z\"," +
            "\"ID\":\"a-9\",\"Trust_Level\":1,\"USERNAME\":\"bob\"}";

        var actor = JsonSerializer.Deserialize<ActorSummary>(mixedCase, Json.Wire);

        Assert.NotNull(actor);
        Assert.Equal("user", actor.ActorType);
        Assert.Equal("bob", actor.Username);
        Assert.Equal(1, actor.TrustLevel);
    }
}