using System.Net;
using Actos.Errors;
using Actos.Models;

namespace Actos.Tests;

/// <summary>
/// Verifies that the real transport maps RFC 9457 problem+json responses to the correct typed
/// <see cref="ActosApiException"/> subclass, dispatching on the body's <c>code</c> field (never on
/// HTTP status). Every assertion goes through a real <see cref="Actos.ActosClient"/> call so the
/// <see cref="ActosExceptionFactory"/> registered in the transport is exercised.
/// </summary>
public class ErrorMappingTests
{
    private static async Task<ActosApiException> ExecuteExpectingError(
        HttpStatusCode status,
        string code,
        string? detail = null,
        string? requestId = "req-test-1")
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.Problem(status, code, detail, requestId));
        using var client = TestHarness.BuildClient(handler);

        try
        {
            await client.RequestAsync<ActorSummary>(HttpMethod.Get, "/test");
        }
        catch (ActosApiException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected an ActosApiException for {status} code={code} but the call succeeded.");
    }

    [Fact]
    public async Task NotFound_404_Maps_To_ActosNotFoundException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.NotFound, "NOT_FOUND", detail: "no such actor");

        Assert.IsType<ActosNotFoundException>(ex);
        Assert.Equal("NOT_FOUND", ex.ErrorCode);
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("req-test-1", ex.RequestId);
        Assert.Equal("no such actor", ex.Detail);
    }

    [Fact]
    public async Task MissingCredentials_401_Maps_To_ActosAuthenticationException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.Unauthorized, "MISSING_CREDENTIALS");

        Assert.IsType<ActosAuthenticationException>(ex);
        Assert.Equal("MISSING_CREDENTIALS", ex.ErrorCode);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task InvalidKey_401_Maps_To_ActosInvalidKeyException_Which_Is_An_AuthenticationException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.Unauthorized, "INVALID_KEY");

        Assert.IsType<ActosInvalidKeyException>(ex);
        Assert.IsAssignableFrom<ActosAuthenticationException>(ex);
        Assert.Equal("INVALID_KEY", ex.ErrorCode);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Forbidden_403_Maps_To_ActosForbiddenException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.Forbidden, "FORBIDDEN");

        Assert.IsType<ActosForbiddenException>(ex);
        Assert.Equal("FORBIDDEN", ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Banned_403_Maps_To_ActosBannedException_Which_Is_A_ForbiddenException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.Forbidden, "BANNED");

        Assert.IsType<ActosBannedException>(ex);
        Assert.IsAssignableFrom<ActosForbiddenException>(ex);
        Assert.Equal("BANNED", ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Gone_410_Maps_To_ActosGoneException()
    {
        var ex = await ExecuteExpectingError(HttpStatusCode.Gone, "GONE");

        Assert.IsType<ActosGoneException>(ex);
        Assert.Equal("GONE", ex.ErrorCode);
        Assert.Equal(410, ex.StatusCode);
    }

    [Fact]
    public async Task UnknownCode_Maps_To_Base_ActosApiException_Only()
    {
        // Use a status the retry handler never retries so exactly one request is made.
        var ex = await ExecuteExpectingError(HttpStatusCode.UnprocessableContent, "SOME_FUTURE_CODE");

        Assert.IsType<ActosApiException>(ex);
        Assert.Equal("SOME_FUTURE_CODE", ex.ErrorCode);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task RequestId_Header_Is_Surfaced_When_Body_Has_No_RequestId()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.Problem(HttpStatusCode.NotFound, "NOT_FOUND", requestId: "req-from-header"));
        using var client = TestHarness.BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ActosNotFoundException>(
            () => client.RequestAsync<ActorSummary>(HttpMethod.Get, "/x"));

        Assert.Equal("req-from-header", ex.RequestId);
    }
}