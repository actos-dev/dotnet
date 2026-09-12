using System.Net.Http.Headers;

namespace Actos.Utils;

/// <summary>
/// An in-memory file to send as a <c>multipart/form-data</c> part — a post/comment image, or an
/// avatar. Construct via <see cref="FromBytes"/>, <see cref="FromStream"/>, or <see cref="FromPath"/>.
/// </summary>
public sealed class FileUpload
{
    private FileUpload(byte[] bytes, string fileName, string? contentType)
    {
        Bytes = bytes;
        FileName = fileName;
        ContentType = contentType;
    }

    internal byte[] Bytes { get; }

    internal string FileName { get; }

    internal string? ContentType { get; }

    /// <summary>Wraps raw in-memory file contents.</summary>
    /// <param name="bytes">Raw file contents.</param>
    /// <param name="fileName">Client file name used in the disposition (for example <c>photo.png</c>).</param>
    /// <param name="contentType">MIME type of the file content, or <see langword="null"/> for <c>application/octet-stream</c>.</param>
    public static FileUpload FromBytes(byte[] bytes, string fileName, string? contentType = null)
        => new(bytes, fileName, contentType);

    /// <summary>
    /// Wraps the contents of a <see cref="Stream"/>. The stream is buffered into memory
    /// immediately so the multipart body can be built (and retried) without re-reading it.
    /// </summary>
    public static FileUpload FromStream(Stream stream, string fileName, string? contentType = null)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new FileUpload(buffer.ToArray(), fileName, contentType);
    }

    /// <summary>Reads a file from disk, deriving the file name from <paramref name="filePath"/>.</summary>
    public static FileUpload FromPath(string filePath, string? contentType = null)
        => new(File.ReadAllBytes(filePath), Path.GetFileName(filePath), contentType);

    internal HttpContent ToHttpContent()
    {
        var content = new ByteArrayContent(Bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(ContentType ?? "application/octet-stream");
        return content;
    }
}
