using System.Text.Json;
using Actos.Models;
using Actos.Transport;

namespace Actos.Tests;

/// <summary>
/// Verifies forward compatibility of the wire serializer: JSON that carries unknown/extra keys that
/// a newer server adds must never break an older client. Unknown members are ignored on read.
/// </summary>
public class ForwardCompatTests
{
    [Fact]
    public void ActorSummary_Ignores_Unknown_Keys_On_Deserialize()
    {
        const string json =
            "{\"actor_type\":\"user\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
            "\"trust_level\":1,\"username\":\"carol\"," +
            "\"future_badge\":\"gold\",\"future_metrics\":{\"karma\":42},\"future_flags\":[true,false]}";

        var actor = JsonSerializer.Deserialize<ActorSummary>(json, Json.Wire);

        Assert.NotNull(actor);
        Assert.Equal("user", actor.ActorType);
        Assert.Equal("carol", actor.Username);
    }

    [Fact]
    public void Wire_Reads_Json_With_Trailing_Comma_And_Comments()
    {
        // Wire options tolerate trailing commas and C-style comments — forward-compatible lenience.
        const string json = """
            {
              "actor_type": "user",
              "created_at": "2026-01-01T00:00:00Z",
              "id": "a-2",
              "trust_level": 1,
              "username": "dave", // a trailing comment
              "unknown_thing": 123,
            }
            """;

        var actor = JsonSerializer.Deserialize<ActorSummary>(json, Json.Wire);

        Assert.NotNull(actor);
        Assert.Equal("dave", actor.Username);
    }

    [Fact]
    public void Wire_Reads_Numbers_From_Strings_When_Expected_As_Numbers()
    {
        // NumberHandling.AllowReadingFromString lets a server emit "2" for an int field.
        const string json =
            "{\"actor_type\":\"user\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-3\"," +
            "\"trust_level\":\"2\",\"username\":\"erin\"}";

        var actor = JsonSerializer.Deserialize<ActorSummary>(json, Json.Wire);

        Assert.NotNull(actor);
        Assert.Equal(2, actor.TrustLevel);
    }
}