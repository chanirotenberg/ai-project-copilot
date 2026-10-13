using FluentValidation;

namespace ProjectCopilot.Application.Documents.UploadDocumentFile;

/// <summary>
/// Structural validation of an upload request. Content inspection (magic bytes / text
/// heuristics) needs to read the stream and therefore lives in the handler.
/// </summary>
public sealed class UploadDocumentFileValidator : AbstractValidator<UploadDocumentFileCommand>
{
    public const int MaxFileNameLength = 255;

    private readonly long _maxFileSizeBytes;

    /// <param name="maxFileSizeBytes">
    /// The configured upload ceiling (DocumentUpload:MaxFileSizeBytes). Checked here as the
    /// authoritative business rule, independently of the transport-level request size limit.
    /// </param>
    public UploadDocumentFileValidator(long maxFileSizeBytes)
    {
        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFileSizeBytes),
                "The maximum document upload size must be greater than zero.");
        }

        _maxFileSizeBytes = maxFileSizeBytes;

        RuleFor(x => x.DocumentId)
            .NotEmpty();

        RuleFor(x => x.Content)
            .NotNull();

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(MaxFileNameLength)
            // Control characters (including NUL) have no legitimate place in a file name and
            // would only ever serve to corrupt logs or downstream consumers of the metadata.
            .Must(fileName => fileName is null || !fileName.Any(char.IsControl))
            .WithMessage("File name must not contain control characters.")
            .Must(fileName => DocumentFileTypes.ResolveAllowedExtension(fileName) is not null)
            .WithMessage(
                "File type is not supported. Allowed file types: "
                + string.Join(", ", DocumentFileTypes.AllowedExtensions)
                + ".");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .WithMessage("File must not be empty.")
            .LessThanOrEqualTo(_ => _maxFileSizeBytes)
            .WithMessage($"File must not exceed {_maxFileSizeBytes} bytes.");
    }
}
