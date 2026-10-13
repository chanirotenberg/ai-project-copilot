namespace ProjectCopilot.Infrastructure.Storage;

/// <summary>
/// Configuration for where uploaded document files are written
/// (configuration section "DocumentStorage").
/// </summary>
public sealed class DocumentStorageOptions
{
    /// <summary>
    /// Absolute path of the directory that holds every uploaded document file. A relative value
    /// is resolved against the application content root during startup. No stored file may ever
    /// resolve outside this directory.
    /// </summary>
    public string RootPath { get; set; } = "";
}
