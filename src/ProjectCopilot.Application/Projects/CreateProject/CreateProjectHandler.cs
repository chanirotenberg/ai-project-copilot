using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Projects.CreateProject;

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
            Deadline = NormalizeToUtc(command.Deadline),
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

    /// <summary>
    /// Ensures a deadline is stored with <see cref="DateTimeKind.Utc"/>, as required by the
    /// PostgreSQL "timestamp with time zone" column. System.Text.Json deserializes a date-only
    /// or timezone-less ISO string (e.g. "2026-11-07", as sent by an HTML &lt;input type="date"&gt;)
    /// with Kind = Unspecified, which Npgsql rejects when writing to a timestamptz column.
    /// </summary>
    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}