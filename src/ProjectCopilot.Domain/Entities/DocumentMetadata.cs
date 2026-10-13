namespace ProjectCopilot.Domain.Entities;

public class DocumentMetadata
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// The file name as supplied by the uploading client. Untrusted display metadata only —
    /// it is never used to build a filesystem path. Null until a file has been uploaded.
    /// </summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// The canonical content type resolved server-side from the validated file extension.
    /// Never the client-declared multipart content type. Null until a file has been uploaded.
    /// </summary>
    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>
    /// The server-generated on-disk file name (a GUID plus an allow-listed extension), relative
    /// to the configured document storage root. Never a full path, never derived from client
    /// input, and never exposed through the API — see DocumentResponse.
    /// </summary>
    public string? StorageFileName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token, mapped to PostgreSQL's own xmin system column.
    /// </summary>
    /// <remarks>
    /// Without it, two concurrent uploads to the same file-less document could both pass the
    /// "does this document already have a file?" check and both commit, leaving one client with
    /// a success response that does not describe the persisted state and one orphaned file on
    /// disk. With it, the losing write fails and is compensated. Mirrors RefreshToken's token.
    /// </remarks>
    public uint RowVersion { get; set; }
}
