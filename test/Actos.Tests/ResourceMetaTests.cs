namespace Actos.Tests;

/// <summary>
/// Exercises the Meta resource: liveness health, readiness, and server version, verifying each
/// method hits the correct path and deserializes its model.
/// </summary>
public class ResourceMetaTests
{
    [Fact]
    public async Task Health_Hits_Health_Path_And_Deserializes_Status()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(200, "{ \"status\": \"ok\" }"));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var meta = new Actos.Resources.MetaResource(transport);

        var health = await meta.HealthAsync();

        Assert.Equal("/health", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("ok", health.Status);
    }

    [Fact]
    public async Task Ready_Hits_Ready_Path_And_Deserializes_Checks()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(
            200,
            "{\"status\":\"ready\",\"database\":{\"status\":\"up\"},\"redis\":{\"status\":\"up\"},\"storage\":{\"status\":\"up\"}}"));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var meta = new Actos.Resources.MetaResource(transport);

        var ready = await meta.ReadyAsync();

        Assert.Equal("/health/ready", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("ready", ready.Status);
        Assert.Equal("up", ready.Database.Status);
        Assert.Equal("up", ready.Redis.Status);
        Assert.Equal("up", ready.Storage.Status);
    }

    [Fact]
    public async Task Version_Hits_Version_Path_And_Deserializes_Members()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(
            200,
            "{\"name\":\"actos-api\",\"version\":\"0.1.0\",\"git_sha\":\"abc1234\",\"api_version\":\"v1\"}"));
        using var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl, TestHarness.TestApiKey, handler, maxRetries: 1);
        var meta = new Actos.Resources.MetaResource(transport);

        var version = await meta.VersionAsync();

        Assert.Equal("/version", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("actos-api", version.Name);
        Assert.Equal("v1", version.ApiVersion);
        Assert.Equal("abc1234", version.GitSha);
        Assert.Equal("0.1.0", version.VersionValue);
    }
}