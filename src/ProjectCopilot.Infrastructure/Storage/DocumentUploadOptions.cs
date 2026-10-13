namespace ProjectCopilot.Infrastructure.Storage;

/// <summary>
/// Document upload limits (configuration section "DocumentUpload").
/// </summary>
public sealed class DocumentUploadOptions
{
    public const long DefaultMaxFileSizeBytes = 20L * 1024 * 1024;

    /// <summary>
    /// Maximum accepted size of a single uploaded document file. Enforced twice: as the
    /// per-route transport request-size limit, and again as the authoritative application-level
    /// rule in <c>UploadDocumentFileValidator</c>.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;
}
