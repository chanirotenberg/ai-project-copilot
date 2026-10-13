namespace ProjectCopilot.Application.Abstractions;

/// <summary>
/// Persists uploaded document file content outside the database.
/// </summary>
/// <remarks>
/// <paramref name="storageFileName"/> on every member is a server-generated bare file name
/// (no directory separators, no relative segments, never client input). Implementations must
/// treat anything else as a programming error and refuse it rather than attempting to resolve it.
/// </remarks>
public interface IDocumentFileStorage
{
    /// <summary>
    /// Writes <paramref name="content"/> under <paramref name="storageFileName"/>. Must not
    /// overwrite an existing entry.
    /// </summary>
    Task SaveAsync(
        Stream content,
        string storageFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes <paramref name="storageFileName"/>. Succeeds if it is already absent.
    /// </summary>
    Task DeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default);
}
