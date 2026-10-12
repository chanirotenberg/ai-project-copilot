using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Projects.CreateProject;

// KNOWN GAP (tracked, not fixed in this pass): Create Project has no idempotency protection.
// Two truly concurrent, identical POST requests from the same user can both succeed and
// create two separate projects (with distinct ids) - only the per-(creator, name) uniqueness
// constraint (see migration AddProjectNameUniquePerCreator) would prevent duplicates, and only
// when the names actually collide. If this needs fixing later, the standard approach is a
// client-supplied idempotency-key header deduplicated server-side.
public sealed class CreateProjectHandler
{
    private readonly IProjectRepository _projectRepository;
    private readonly IValidator<CreateProjectCommand> _validator;

    public CreateProjectHandler(
        IProjectRepository projectRepository,
        IValidator<CreateProjectCommand> validator)
    {
        _projectRepository = projectRepository;
        _validator = validator;
    }

    public async Task<Project> HandleAsync(
        CreateProjectCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var now = DateTime.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            Status = "Active",
            Deadline = DateTimeNormalization.NormalizeToUtc(command.Deadline),
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _projectRepository.AddAsync(project, cancellationToken);

        await _projectRepository.AddMemberAsync(
            new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId,
                CreatedAtUtc = now
            },
            cancellationToken);

        await _projectRepository.SaveChangesAsync(cancellationToken);

        return project;
    }
}