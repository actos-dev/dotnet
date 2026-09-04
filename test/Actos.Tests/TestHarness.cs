using System.Net;
using System.Text;
using System.Text.Json;
using Actos;
using Actos.Models;

namespace Actos.Tests;

/// <summary>
/// A hand-written, scripted <see cref="HttpMessageHandler"/> used to drive the real
/// Actos transport pipeline without any external mocking library. Tests enqueue the exact
/// <see cref="HttpResponseMessage"/> responses the handler should return, optionally making a
/// particular attempt throw a transport-level exception (to exercise the retry path).
/// </summary>
public sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    /// <summary>
    /// When set, <see cref="SendAsync"/> calls this for the current (1-based) attempt. If it
    /// returns a non-null exception, that exception is thrown instead of returning a response.
    /// </summary>
    public Func<int, Exception?>? ThrowError { get; set; }

    /// <summary>Number of <see cref="SendAsync"/> invocations observed so far.</summary>
    public int RequestCount { get; private set; }

    /// <summary>The most recent request dispatched through this handler.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The string body of the most recent request (captured before disposal), or <see langword="null"/>.</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>Queues a response that will be returned on the next send.</summary>
    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        LastRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        if (ThrowError?.Invoke(RequestCount) is { } error)
        {
            throw error;
        }

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                "ScriptedHttpMessageHandler has no more responses queued; the request pipeline made an unexpected call.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

/// <summary>
/// Shared construction helpers: builds real <see cref="ActosClient"/> instances wired to a
/// <see cref="ScriptedHttpMessageHandler"/> and builds RFC 9457 <c>application/problem+json</c>
/// responses plus well-formed 2xx payloads.
/// </summary>
internal static class TestHarness
{
    public const string TestBaseUrl = "http://localhost:9999";
    public const string TestApiKey = "actos_test_key";

    /// <summary>A canonical <c>ActorSummary</c> JSON body used for 2xx responses.</summary>
    public const string ActorJson =
        "{\"actor_type\":\"user\",\"created_at\":\"2026-01-01T00:00:00Z\",\"id\":\"a-1\"," +
        "\"trust_level\":2,\"username\":\"alice\",\"avatar_url\":\"http://example/av.png\"," +
        "\"bio\":null,\"display_name\":\"Alice\",\"unexpected_future_field\":\"ignored\"}";

    /// <summary>Builds a real <see cref="ActosClient"/> whose innermost handler is the scripted one.</summary>
    public static ActosClient BuildClient(ScriptedHttpMessageHandler handler, int maxRetries = 1, string? apiKey = TestApiKey)
    {
        var options = new ActosClientOptions
        {
            HttpMessageHandler = handler,
            MaxRetries = maxRetries,
        };

        return new ActosClient(apiKey, TestBaseUrl, options);
    }

    public static HttpResponseMessage JsonResponse(int status, string json, string contentType = "application/json")
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(json, Encoding.UTF8, contentType),
        };

        return response;
    }

    /// <summary>Builds an RFC 9457 <c>application/problem+json</c> response.</summary>
    public static HttpResponseMessage Problem(
        HttpStatusCode status,
        string code,
        string? detail = null,
        string? requestId = null,
        string title = "An error occurred")
    {
        var builder = new StringBuilder();
        builder.Append("{\"type\":\"about:blank\",\"title\":").Append(JsonSerializer.Serialize(title))
            .Append(",\"status\":").Append((int)status).Append(",\"code\":").Append(JsonSerializer.Serialize(code));
        if (detail is not null)
        {
            builder.Append(",\"detail\":").Append(JsonSerializer.Serialize(detail));
        }

        if (requestId is not null)
        {
            builder.Append(",\"request_id\":").Append(JsonSerializer.Serialize(requestId));
        }

        builder.Append('}');

        var response = JsonResponse((int)status, builder.ToString(), "application/problem+json");
        if (requestId is not null)
        {
            response.Headers.TryAddWithoutValidation("x-request-id", requestId);
        }

        return response;
    }

    public static ActorSummary SampleActor()
    {
        return new ActorSummary(
            ActorType: "user",
            CreatedAt: "2026-01-01T00:00:00Z",
            Id: "a-1",
            TrustLevel: 2,
            Username: "alice",
            AvatarUrl: "http://example/av.png",
            Bio: null,
            DisplayName: "Alice");
    }
}