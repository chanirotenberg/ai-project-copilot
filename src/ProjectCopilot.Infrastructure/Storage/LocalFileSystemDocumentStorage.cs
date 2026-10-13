using Microsoft.Extensions.Options;
using ProjectCopilot.Application.Abstractions;

namespace ProjectCopilot.Infrastructure.Storage;

/// <summary>
/// Stores uploaded document files as plain files under a single configured root directory.
/// </summary>
/// <remarks>
/// Confinement to the root is structural rather than sanitised: the only thing combined with the
/// root is a bare, server-generated file name, so there is nothing for a caller to influence.
/// <see cref="ResolveFullPath"/> additionally asserts the resolved path really is inside the
/// root, so a future caller that passed something path-like would be refused rather than
/// silently escaping.
/// </remarks>
public sealed class LocalFileSystemDocumentStorage : IDocumentFileStorage
{
    private const int CopyBufferSize = 81920;

    private readonly DocumentStorageOptions _options;

    public LocalFileSystemDocumentStorage(IOptions<DocumentStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task SaveAsync(
        Stream content,
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = ResolveFullPath(storageFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // FileMode.CreateNew: never overwrite. A name collision is effectively impossible for a
        // fresh GUID, and if one ever happened, failing is correct — silently replacing another
        // document's file would not be.
        await using var target = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            useAsync: true);

        await content.CopyToAsync(target, CopyBufferSize, cancellationToken);
    }

    public Task DeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveFullPath(storageFileName);

        // Deleting an already-absent file is a success: the caller's intent is "it must not be
        // there", and that is satisfied.
        File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storageFileName)
    {
        // Both separator characters are rejected regardless of host platform: on Linux a
        // backslash is an ordinary file-name character, and relying on platform-specific parsing
        // would make this guard behave differently per deployment target.
        if (string.IsNullOrWhiteSpace(storageFileName)
            || storageFileName.Contains('/')
            || storageFileName.Contains('\\')
            || !string.Equals(storageFileName, Path.GetFileName(storageFileName), StringComparison.Ordinal)
            || storageFileName.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(storageFileName))
        {
            throw new InvalidOperationException(
                "A document storage file name must be a bare file name.");
        }

        var root = Path.GetFullPath(_options.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, storageFileName));

        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        // Defence in depth: both sides come from Path.GetFullPath over the same root, so this can
        // only trip if the guards above were ever weakened.
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The resolved document storage path escapes the configured storage root.");
        }

        return fullPath;
    }
}
