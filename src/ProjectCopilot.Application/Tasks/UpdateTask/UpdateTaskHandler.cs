using ProjectCopilot.Application.Common.Exceptions;
using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tasks.UpdateTask;

public sealed class UpdateTaskHandler
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IValidator<UpdateTaskCommand> _validator;

    public UpdateTaskHandler(
        ITaskRepository taskRepository,
        IProjectMembershipService membershipService,
        IValidator<UpdateTaskCommand> validator)
    {
        _taskRepository = taskRepository;
        _membershipService = membershipService;
        _validator = validator;
    }

    public async Task<TaskItem> HandleAsync(
        UpdateTaskCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var task = await _taskRepository.GetByIdAsync(
            command.Id,
            cancellationToken);

        if (task is null)
        {
            throw new NotFoundException(
                $"Task with id '{command.Id}' was not found.");
        }

        await _membershipService.EnsureMemberAsync(
            task.ProjectId,
            userId,
            cancellationToken);

        task.Title = command.Title.Trim();
        task.Description = command.Description?.Trim();
        task.Status = command.Status;
        task.Priority = command.Priority;
        task.AssignedUserId = command.AssignedUserId;
        task.DueDate = NormalizeToUtc(command.DueDate);
        task.IsBlocked = command.IsBlocked;
        task.UpdatedAt = DateTime.UtcNow;

        await _taskRepository.SaveChangesAsync(cancellationToken);

        return task;
    }

    /// <summary>
    /// Ensures a due date is stored with <see cref="DateTimeKind.Utc"/>, as required by the
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