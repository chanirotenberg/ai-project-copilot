using ProjectCopilot.Application.Common.Exceptions;
using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Documents.CreateDocument;

public sealed class CreateDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IValidator<CreateDocumentCommand> _validator;

    public CreateDocumentHandler(
        IDocumentRepository documentRepository,
        IProjectRepository projectRepository,
        IProjectMembershipService membershipService,
        IValidator<CreateDocumentCommand> validator)
    {
        _documentRepository = documentRepository;
        _projectRepository = projectRepository;
        _membershipService = membershipService;
        _validator = validator;
    }

    public async Task<DocumentMetadata> HandleAsync(
        CreateDocumentCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var project = await _projectRepository.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            // Must stay textually identical to the membership-check 404 below — otherwise a
            // caller probing command.ProjectId values could tell "doesn't exist" apart from
            // "exists but you're not a member" purely from the response body text.
            throw NotFoundException.HiddenResource();
        }

        await _membershipService.EnsureMemberAsync(
            command.ProjectId,
            userId,
            cancellationToken);

        var now = DateTime.UtcNow;

        var document = new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            ProjectId = command.ProjectId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _documentRepository.AddAsync(document, cancellationToken);
        await _documentRepository.SaveChangesAsync(cancellationToken);

        return document;
    }
}
