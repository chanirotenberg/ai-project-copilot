using System.Text;
using Microsoft.Extensions.Options;
using ProjectCopilot.Infrastructure.Storage;

namespace ProjectCopilot.IntegrationTests.Documents;

/// <summary>
/// Confinement and overwrite behaviour of the local document file store, exercised against a
/// real throwaway directory rather than the development storage root.
/// </summary>
public class LocalFileSystemDocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "projectcopilot-storage-unit",
        Guid.NewGuid().ToString("N"));

    private LocalFileSystemDocumentStorage CreateStorage() =>
        new(Options.Create(new DocumentStorageOptions { RootPath = _root }));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Throwaway temp directory; a leftover is harmless.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateTheRootAndWriteTheFileInsideIt()
    {
        var storage = CreateStorage();
        var payload = Encoding.UTF8.GetBytes("stored content");

        await storage.SaveAsync(new MemoryStream(payload), "abc123.txt");

        var expectedPath = Path.Combine(_root, "abc123.txt");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(expectedPath));
    }

    [Fact]
    public async Task SaveAsync_WhenTheNameAlreadyExists_ShouldRefuseRatherThanOverwrite()
    {
        var storage = CreateStorage();

        await storage.SaveAsync(new MemoryStream([1, 2, 3]), "collision.txt");

        await Assert.ThrowsAsync<IOException>(() =>
            storage.SaveAsync(new MemoryStream([9, 9, 9]), "collision.txt"));

        Assert.Equal(
            new byte[] { 1, 2, 3 },
            await File.ReadAllBytesAsync(Path.Combine(_root, "collision.txt")));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("nested/escape.txt")]
    [InlineData("nested\\escape.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WithANonBareFileName_ShouldRefuse(string storageFileName)
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SaveAsync(new MemoryStream([1]), storageFileName));
    }

    [Fact]
    public async Task SaveAsync_WithARootedPath_ShouldRefuse()
    {
        var storage = CreateStorage();
        var rooted = Path.Combine(Path.GetTempPath(), "rooted-escape.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.SaveAsync(new MemoryStream([1]), rooted));

        Assert.False(File.Exists(rooted));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveOnlyTheNamedFile()
    {
        var storage = CreateStorage();

        await storage.SaveAsync(new MemoryStream([1]), "target.txt");
        await storage.SaveAsync(new MemoryStream([2]), "bystander.txt");

        await storage.DeleteAsync("target.txt");

        Assert.False(File.Exists(Path.Combine(_root, "target.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "bystander.txt")));
    }

    [Fact]
    public async Task DeleteAsync_WhenTheFileIsAlreadyAbsent_ShouldSucceed()
    {
        var storage = CreateStorage();
        Directory.CreateDirectory(_root);

        await storage.DeleteAsync("never-existed.txt");
    }

    [Fact]
    public async Task DeleteAsync_WithANonBareFileName_ShouldRefuse()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.DeleteAsync("../escape.txt"));
    }
}
