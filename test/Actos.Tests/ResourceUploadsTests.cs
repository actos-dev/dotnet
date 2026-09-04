using System.Net;
using System.Text;

namespace Actos.Tests;

/// <summary>
/// Exercises the Uploads resource: multipart upload (field name <c>file</c>), the stream convenience
/// API, and the no-content delete. The scripted handler captures the multipart body by serializing it,
/// so assertions here read the captured multipart string.
/// </summary>
public class ResourceUploadsTests
{
    private const string UploadJson =
        "{\"id\":\"f-1\",\"url\":\"http://example/f-1.png\",\"thumbnail_url\":\"http://example/f-1-t.png\"," +
        "\"mime_type\":\"text/plain\",\"byte_size\":5,\"width\":null,\"height\":null," +
        "\"checksum_sha256\":\"2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824\"," +
        "\"created_at\":\"2026-01-01T00:00:00Z\"}";

    private static Actos.Resources.UploadsResource Build(ScriptedHttpMessageHandler handler)
    {
        var transport = new Actos.Transport.Transport(
            TestHarness.TestBaseUrl,
            TestHarness.TestApiKey,
            handler,
            maxRetries: 1);
        return new Actos.Resources.UploadsResource(transport);
    }

    [Fact]
    public async Task UploadAsync_Sends_Multipart_With_Field_Named_File()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, UploadJson));
        var resource = Build(handler);

        var upload = await resource.UploadAsync(
            Encoding.UTF8.GetBytes("hello"),
            "diagram.txt",
            "text/plain");

        // Response is deserialized from the wire payload.
        Assert.Equal("f-1", upload.Id);
        Assert.Equal("http://example/f-1.png", upload.Url);
        Assert.Equal("text/plain", upload.MimeType);
        Assert.Equal(5, upload.ByteSize);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", upload.ChecksumSha256);

        Assert.Equal("/uploads", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);

        // The body is multipart/form-data, not JSON. The handler serialized it to a string;
        // token Content-Disposition values are serialized unquoted (name=file, filename=diagram.txt).
        var body = handler.LastRequestBody!;
        Assert.StartsWith("multipart/form-data; boundary=", handler.LastRequest.Content!.Headers.ContentType!.ToString());
        Assert.Contains("name=file", body);
        Assert.Contains("filename=diagram.txt", body);
        Assert.Contains("Content-Type: text/plain", body);
        Assert.Contains("hello", body);
    }

    [Fact]
    public async Task UploadAsync_Stream_Buffers_Into_File_Field()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(TestHarness.JsonResponse(201, UploadJson));
        var resource = Build(handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("stream-bytes"));
        var upload = await resource.UploadAsync(stream, "stream.txt", "text/plain");

        Assert.Equal("f-1", upload.Id);
        var body = handler.LastRequestBody!;
        Assert.Contains("name=file", body);
        Assert.Contains("filename=stream.txt", body);
        Assert.Contains("stream-bytes", body);
    }

    [Fact]
    public async Task DeleteAsync_Is_NoContent()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var resource = Build(handler);

        await resource.DeleteAsync("f-1");

        Assert.Equal("/uploads/f-1", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }
}