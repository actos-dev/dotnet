using Actos.Models;

namespace Actos.Resources;

/// <summary>
/// File upload lifecycle: upload a file as <c>multipart/form-data</c> (field <c>file</c>, 8MB limit)
/// and delete an upload by id. Uploading and binding to a post are separate steps — feed the returned
/// <see cref="UploadResponse.Id"/> into a Create/Update <c>attachmentIds</c> argument.
/// </summary>
public sealed class UploadsResource
{
    private const string FieldName = "file";

    private readonly Actos.Transport.Transport _transport;

    internal UploadsResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Uploads an in-memory file as <c>multipart/form-data</c> under the field name <c>file</c>.
    /// </summary>
    /// <param name="bytes">Raw file contents.</param>
    /// <param name="fileName">Client file name used in the disposition (for example <c>photo.png</c>).</param>
    /// <param name="contentType">MIME type of the file content, or <see langword="null"/> for <c>application/octet-stream</c>.</param>
    public Task<UploadResponse> UploadAsync(
        byte[] bytes,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType ?? "application/octet-stream");

        using var form = new MultipartFormDataContent();
        form.Add(file, FieldName, fileName);

        return _transport.RequestAsync<UploadResponse>(
            HttpMethod.Post,
            "/uploads",
            form,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Uploads the contents of a <see cref="Stream"/>. The stream is buffered into memory before
    /// sending so multipart retries can rewind reliably.
    /// </summary>
    public Task<UploadResponse> UploadAsync(
        Stream stream,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return UploadAsync(buffer.ToArray(), fileName, contentType, cancellationToken);
    }

    /// <summary>
    /// Uploads a file from disk, deriving the file name from <paramref name="filePath"/>.
    /// </summary>
    public Task<UploadResponse> UploadAsync(
        string filePath,
        string? contentType = null,
        CancellationToken cancellationToken = default)
        => UploadAsync(File.ReadAllBytes(filePath), Path.GetFileName(filePath), contentType, cancellationToken);

    /// <summary>Deletes an upload by id. The caller typically attaches uploads to a post <i>before</i> deleting.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(HttpMethod.Delete, $"/uploads/{id}", cancellationToken: cancellationToken);
}