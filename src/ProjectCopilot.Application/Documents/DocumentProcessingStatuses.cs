namespace ProjectCopilot.Application.Documents;

/// <summary>
/// The processing lifecycle statuses a document may carry, and the single place their literal
/// values are defined.
/// </summary>
/// <remarks>
/// Stored as plain strings on <c>DocumentMetadata.Status</c>, following the same convention as
/// the existing project and task statuses rather than introducing a database enum.
///
/// A null status is not listed here: it means no file has been uploaded yet, which is the
/// absence of a processing status rather than a fifth one.
/// </remarks>
public static class DocumentProcessingStatuses
{
    /// <summary>A file is attached and is waiting to be processed.</summary>
    public const string Uploaded = "Uploaded";

    /// <summary>Processing has started and has not finished.</summary>
    public const string Processing = "Processing";

    /// <summary>Processing finished successfully.</summary>
    public const string Ready = "Ready";

    /// <summary>Processing failed.</summary>
    public const string Failed = "Failed";

    /// <summary>
    /// Every non-null status a document may hold. Used to reject an unknown status value before
    /// the legal-transition rules are consulted at all.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Uploaded,
        Processing,
        Ready,
        Failed
    ];

    /// <summary>
    /// Whether <paramref name="status"/> is one of the known non-null statuses. Ordinal and
    /// case-sensitive: the stored values are exact literals, not user-entered text.
    /// </summary>
    public static bool IsKnown(string? status) =>
        status is not null && All.Contains(status, StringComparer.Ordinal);
}
