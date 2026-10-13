namespace ProjectCopilot.Application.Documents.UploadDocumentFile;

/// <summary>
/// The closed allow-list of document file types accepted for upload, and the canonical content
/// type stored for each.
/// </summary>
/// <remarks>
/// This is an allow-list, not a deny-list: anything not listed here is rejected. The extension
/// values here are the only extensions ever appended to a generated storage file name — the
/// client's own string is matched against this list and then discarded, so no part of a client
/// filename can reach the filesystem.
/// </remarks>
public static class DocumentFileTypes
{
    public const string Pdf = ".pdf";
    public const string Docx = ".docx";
    public const string Txt = ".txt";
    public const string Markdown = ".md";
    public const string Csv = ".csv";

    public static readonly IReadOnlyList<string> AllowedExtensions =
    [
        Pdf,
        Docx,
        Txt,
        Markdown,
        Csv
    ];

    /// <summary>
    /// Returns the allow-listed extension matching <paramref name="fileName"/>'s extension, or
    /// null when the extension is absent or not allow-listed.
    /// </summary>
    /// <remarks>
    /// The returned value is the allow-list's own literal, never a slice of
    /// <paramref name="fileName"/>. The comparison deliberately works on the raw string without
    /// any path parsing, so a traversal-style name such as "../../evil.pdf" simply resolves to
    /// ".pdf" with no path semantics applied.
    /// </remarks>
    public static string? ResolveAllowedExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var lastDot = fileName.LastIndexOf('.');

        if (lastDot < 0 || lastDot == fileName.Length - 1)
        {
            return null;
        }

        var candidate = fileName[lastDot..];

        foreach (var allowed in AllowedExtensions)
        {
            if (string.Equals(candidate, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return null;
    }

    /// <summary>
    /// The content type recorded for an allow-listed extension. Resolved server-side; the
    /// client-declared multipart content type is never stored.
    /// </summary>
    public static string CanonicalContentType(string allowedExtension) => allowedExtension switch
    {
        Pdf => "application/pdf",
        Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        Txt => "text/plain",
        Markdown => "text/markdown",
        Csv => "text/csv",
        _ => throw new ArgumentOutOfRangeException(
            nameof(allowedExtension),
            "Extension is not part of the document upload allow-list.")
    };
}
