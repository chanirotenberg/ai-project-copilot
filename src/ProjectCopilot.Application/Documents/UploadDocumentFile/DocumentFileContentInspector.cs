namespace ProjectCopilot.Application.Documents.UploadDocumentFile;

/// <summary>
/// Inspects the leading bytes of an uploaded file to confirm the content genuinely matches the
/// allow-listed extension it claims.
/// </summary>
/// <remarks>
/// The client-declared multipart content type is never consulted here — it is informational
/// only. For PDF and DOCX the magic-byte signature is authoritative. TXT/MD/CSV have no reliable
/// signature, so they are gated by a conservative "does this look like text" heuristic instead.
/// Every path fails closed: an inconclusive inspection is a rejection, never a best-effort
/// acceptance.
/// </remarks>
public static class DocumentFileContentInspector
{
    /// <summary>
    /// Number of leading bytes the handler should hand to <see cref="MatchesExtension"/>.
    /// </summary>
    public const int HeadByteCount = 8 * 1024;

    /// <summary>
    /// Share of non-printable bytes above which a text-family file is considered binary.
    /// </summary>
    private const double MaxNonPrintableRatio = 0.10;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    // DOCX is an OOXML package, i.e. a ZIP archive: the stream starts with a ZIP local file
    // header. This does not prove the archive is a Word document, only that it is a real ZIP —
    // which is as far as a header check can go, and enough to reject non-archive payloads.
    private static readonly byte[] ZipLocalFileHeaderSignature = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// Returns true when <paramref name="head"/> is consistent with
    /// <paramref name="allowedExtension"/>.
    /// </summary>
    public static bool MatchesExtension(string allowedExtension, ReadOnlySpan<byte> head) =>
        allowedExtension switch
        {
            DocumentFileTypes.Pdf => head.StartsWith(PdfSignature),
            DocumentFileTypes.Docx => head.StartsWith(ZipLocalFileHeaderSignature),
            DocumentFileTypes.Txt or DocumentFileTypes.Markdown or DocumentFileTypes.Csv =>
                LooksLikeText(head),
            _ => false
        };

    /// <summary>
    /// Conservative binary-content detector for the text family.
    /// </summary>
    /// <remarks>
    /// A single NUL byte is an immediate rejection — no text encoding this API accepts produces
    /// one. Bytes at or above 0x80 are counted as printable because valid UTF-8 (Hebrew, and any
    /// other non-ASCII content) is made of them; classifying them as binary would reject
    /// legitimate documents.
    /// </remarks>
    private static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty)
        {
            return false;
        }

        var nonPrintable = 0;

        foreach (var value in head)
        {
            if (value == 0x00)
            {
                return false;
            }

            if (IsNonPrintable(value))
            {
                nonPrintable++;
            }
        }

        return (double)nonPrintable / head.Length <= MaxNonPrintableRatio;
    }

    private static bool IsNonPrintable(byte value) =>
        value switch
        {
            // Tab, line feed, form feed and carriage return are ordinary in text documents.
            0x09 or 0x0A or 0x0C or 0x0D => false,
            < 0x20 => true,
            0x7F => true,
            _ => false
        };
}
