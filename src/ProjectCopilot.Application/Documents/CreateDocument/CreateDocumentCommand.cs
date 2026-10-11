namespace ProjectCopilot.Application.Documents.CreateDocument;

public sealed record CreateDocumentCommand(
    Guid ProjectId,
    string Title,
    string? Description);
