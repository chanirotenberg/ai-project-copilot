using ProjectCopilot.Application.Common.Exceptions;
using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Documents.UpdateDocument;

public sealed class UpdateDocumentHandler
{
    /// <summary>
    /// Reported when another writer updated the document between this request reading it and
    /// saving. Phrased for the person who hit it, since two members editing the same document is
    /// an ordinary thing to do rather than an error.
    /// </summary>
    public const string ConcurrentlyModifiedMessage =
        "This document was modified by someone else. Reload it and try again.";

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
            // Must stay textually identical to the membership-check 404 below — see
            // NotFoundException.HiddenResource() for why this can't embed the requested id.
            throw NotFoundException.HiddenResource();
        }

        await _membershipService.EnsureMemberAsync(
            document.ProjectId,
            userId,
            cancellationToken);

        document.Title = command.Title.Trim();
        document.Description = command.Description?.Trim();
        document.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _documentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // DocumentMetadata carries an optimistic concurrency token, so a concurrent edit by
            // another project member is now detected instead of silently overwriting their
            // change. Two people editing the same document is an everyday occurrence, not a
            // fault, so it is reported as a 409 the caller can act on rather than being allowed
            // to fall through to the generic 500 handler (which would also log it at Error
            // level and pollute alerting).
            throw new ConflictException(ConcurrentlyModifiedMessage);
        }

        return document;
    }
}
