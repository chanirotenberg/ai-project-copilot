namespace ProjectCopilot.Application.Documents.UploadDocumentFile;

/// <summary>
/// Attaches a single uploaded file to an existing document.
/// </summary>
/// <param name="DocumentId">
/// The target document, taken from the route. The project is derived server-side from this
/// document — the command deliberately has no project id for a caller to spoof.
/// </param>
/// <param name="FileName">The untrusted client-supplied file name, stored as metadata only.</param>
/// <param name="SizeBytes">The actual byte length of the bound file.</param>
/// <param name="Content">
/// The uploaded content. Must be seekable: the handler inspects the leading bytes and then
/// rewinds before handing the stream to storage.
/// </param>
public sealed record UploadDocumentFileCommand(
    Guid DocumentId,
    string FileName,
    long SizeBytes,
    Stream Content);
