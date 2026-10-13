using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Documents.UploadDocumentFile;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tests.Documents;

/// <summary>
/// In-memory document repository. Returns the stored entity instance itself, so a test can
/// observe exactly which fields the handler mutated and when.
/// </summary>
internal sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, DocumentMetadata> _documents = [];

    public int SaveChangesCallCount { get; private set; }

    public Exception? SaveChangesException { get; set; }

    public void Seed(DocumentMetadata document) => _documents[document.Id] = document;

    public Task<DocumentMetadata?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.TryGetValue(id, out var document) ? document : null);

    public Task<IReadOnlyList<DocumentMetadata>> GetByProjectIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DocumentMetadata>>(
            _documents.Values.Where(d => d.ProjectId == projectId).ToList());

    public Task AddAsync(DocumentMetadata document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;

        return Task.CompletedTask;
    }

    public void Delete(DocumentMetadata document) => _documents.Remove(document.Id);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        return SaveChangesException is null
            ? Task.CompletedTask
            : Task.FromException(SaveChangesException);
    }
}

/// <summary>
/// Membership service fake. Defaults to denying access, so a test must opt a user in explicitly
/// — a forgotten setup fails closed rather than silently granting access.
/// </summary>
internal sealed class FakeProjectMembershipService : IProjectMembershipService
{
    private readonly HashSet<(Guid ProjectId, Guid UserId)> _memberships = [];

    public List<(Guid ProjectId, Guid UserId)> EnsureMemberCalls { get; } = [];

    public void AddMember(Guid projectId, Guid userId) => _memberships.Add((projectId, userId));

    public Task EnsureMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        EnsureMemberCalls.Add((projectId, userId));

        return _memberships.Contains((projectId, userId))
            ? Task.CompletedTask
            : Task.FromException(NotFoundException.HiddenResource());
    }

    public Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_memberships.Contains((projectId, userId)));
}

internal sealed class FakeDocumentFileStorage : IDocumentFileStorage
{
    public Dictionary<string, byte[]> SavedFiles { get; } = [];

    public List<string> DeletedFileNames { get; } = [];

    public Exception? SaveException { get; set; }

    public Exception? DeleteException { get; set; }

    public async Task SaveAsync(
        Stream content,
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        SavedFiles[storageFileName] = buffer.ToArray();
    }

    public Task DeleteAsync(
        string storageFileName,
        CancellationToken cancellationToken = default)
    {
        DeletedFileNames.Add(storageFileName);

        if (DeleteException is not null)
        {
            return Task.FromException(DeleteException);
        }

        SavedFiles.Remove(storageFileName);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Minimal byte payloads that are genuinely valid for each allow-listed extension, plus
/// deliberately mismatched ones.
/// </summary>
internal static class DocumentFileSamples
{
    public static byte[] Pdf() => "%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n"u8.ToArray();

    // A real ZIP local file header, which is what an OOXML (.docx) package starts with.
    public static byte[] Docx() =>
    [
        0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00,
        0x08, 0x00, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00
    ];

    public static byte[] Text() => "Ordinary text content.\nשורה בעברית, to cover UTF-8 bytes.\n"u8.ToArray();

    public static byte[] Markdown() => "# Heading\n\nSome *markdown* text.\n"u8.ToArray();

    public static byte[] Csv() => "id,name\n1,first\n2,second\n"u8.ToArray();

    /// <summary>Binary content with a NUL byte, which no text-family file may contain.</summary>
    public static byte[] BinaryBlob() => [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE, 0x00, 0x7F];

    public static byte[] ForExtension(string extension) => extension switch
    {
        DocumentFileTypes.Pdf => Pdf(),
        DocumentFileTypes.Docx => Docx(),
        DocumentFileTypes.Txt => Text(),
        DocumentFileTypes.Markdown => Markdown(),
        DocumentFileTypes.Csv => Csv(),
        _ => throw new ArgumentOutOfRangeException(nameof(extension))
    };
}
