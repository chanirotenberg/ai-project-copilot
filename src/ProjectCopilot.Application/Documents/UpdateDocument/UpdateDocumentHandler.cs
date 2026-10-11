using ProjectCopilot.Application.Common.Exceptions;
using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Documents.UpdateDocument;

public sealed class UpdateDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IValidator<UpdateDocumentCommand> _validator;

    public UpdateDocumentHandler(
        IDocumentRepository documentRepository,
        IProjectMembershipService membershipService,
        IValidator<UpdateDocumentCommand> validator)
    {
        _documentRepository = documentRepository;
        _membershipService = membershipService;
        _validator = validator;
    }

    public async Task<DocumentMetadata> HandleAsync(
        UpdateDocumentCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var document = await _documentRepository.GetByIdAsync(
            command.Id,
            cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(
                $"Document with id '{command.Id}' was not found.");
        }

        await _membershipService.EnsureMemberAsync(
            document.ProjectId,
            userId,
            cancellationToken);

        document.Title = command.Title.Trim();
        document.Description = command.Description?.Trim();
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.SaveChangesAsync(cancellationToken);

        return document;
    }
}
