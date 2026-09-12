using System.Text;
using System.Text.Json;
using Actos.Utils;

namespace Actos.Transport;

/// <summary>
/// Builds the <c>multipart/form-data</c> body used by <c>POST /posts</c> and
/// <c>POST /posts/{id}/comments</c> when images travel with the content: one JSON part named
/// <c>payload</c> carrying the same body the <c>application/json</c> path would send, plus up
/// to <see cref="MaxFiles"/> file parts named <c>files</c>.
/// </summary>
public static class MultipartRequest
{
    /// <summary>Maximum number of image files a single post or comment may carry.</summary>
    public const int MaxFiles = 4;

    /// <summary>
    /// Builds the multipart body: <paramref name="payload"/> serialized as the <c>payload</c>
    /// part, and each of <paramref name="files"/> as a <c>files</c> part.
    /// </summary>
    public static MultipartFormDataContent BuildPayloadWithFiles(object payload, IReadOnlyCollection<FileUpload> files)
    {
        if (files.Count > MaxFiles)
        {
            throw new ArgumentException($"At most {MaxFiles} files may be attached, got {files.Count}.", nameof(files));
        }

        var form = new MultipartFormDataContent
        {
            { new StringContent(JsonSerializer.Serialize(payload, Json.Wire), Encoding.UTF8, "application/json"), "payload" },
        };

        foreach (var file in files)
        {
            form.Add(file.ToHttpContent(), "files", file.FileName);
        }

        return form;
    }
}
