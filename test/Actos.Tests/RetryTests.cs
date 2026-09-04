using System.Net;
using System.Net.Http.Headers;
using Actos.Errors;
using Actos.Models;

namespace Actos.Tests;

/// <summary>
/// Verifies the retry behaviour of the real transport: HTTP 429 is retried (honoring Retry-After),
/// non-retryable 4xx responses are never retried, and transport-level failures are retried.
/// The scripted handler records request counts so we assert on call volume, not on timing.
/// </summary>
public class RetryTests
{
    [Fact]
    public async Task Http429_With_RetryAfter_Is_Retried_And_Eventually_Succeeds()
    {
        var handler = new ScriptedHttpMessageHandler();

        // First response: 429 with an immediate Retry-After so the backoff is zero.
        var throttled = TestHarness.Problem(HttpStatusCode.TooManyRequests, "RATE_LIMITED", "slow down");
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        handler.Enqueue(throttled);
        handler.Enqueue(TestHarness.JsonResponse(200, TestHarness.ActorJson));

        using var client = TestHarness.BuildClient(handler, maxRetries: 1);

        var actor = await client.RequestAsync<ActorSummary>(HttpMethod.Get, "/actors/a-1");

        Assert.Equal("alice", actor.Username);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Http400_Is_Not_Retried_And_ValidationException_Surfaces()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.Problem(HttpStatusCode.BadRequest, "VALIDATION_FAILED", "bad field"));
        handler.Enqueue(TestHarness.JsonResponse(200, TestHarness.ActorJson));

        using var client = TestHarness.BuildClient(handler, maxRetries: 1);

        var ex = await Assert.ThrowsAsync<ActosValidationException>(
            () => client.RequestAsync<ActorSummary>(HttpMethod.Get, "/actors/a-1"));

        Assert.Equal("VALIDATION_FAILED", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
        // A 4xx is never retried: exactly one request left the handler.
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Transport_Failure_Is_Retried_And_Eventually_Succeeds()
    {
        var handler = new ScriptedHttpMessageHandler
        {
            // First attempt hits a connection-level failure; second attempt succeeds.
            ThrowError = attempt => attempt == 1 ? new HttpRequestException("connection refused") : null,
        };
        handler.Enqueue(TestHarness.JsonResponse(200, TestHarness.ActorJson));

        using var client = TestHarness.BuildClient(handler, maxRetries: 1);

        var actor = await client.RequestAsync<ActorSummary>(HttpMethod.Get, "/actors/a-1");

        Assert.Equal("alice", actor.Username);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task NonRetryable_4xx_Is_Returned_Even_When_More_Responses_Are_Queued()
    {
        // Guard: a 404 must be surfaced as-is and must not consume the queued 200.
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.Problem(HttpStatusCode.NotFound, "NOT_FOUND"));
        handler.Enqueue(TestHarness.JsonResponse(200, TestHarness.ActorJson));

        using var client = TestHarness.BuildClient(handler, maxRetries: 1);

        await Assert.ThrowsAsync<ActosNotFoundException>(
            () => client.RequestAsync<ActorSummary>(HttpMethod.Get, "/actors/missing"));

        Assert.Equal(1, handler.RequestCount);
    }
}