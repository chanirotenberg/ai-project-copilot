using ProjectCopilot.Application.Common.Exceptions;
using FluentValidation;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Domain.Entities;

namespace ProjectCopilot.Application.Tasks.CreateTask;

public sealed class CreateTaskHandler
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectMembershipService _membershipService;
    private readonly IValidator<CreateTaskCommand> _validator;

    public CreateTaskHandler(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        IProjectMembershipService membershipService,
        IValidator<CreateTaskCommand> validator)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _membershipService = membershipService;
        _validator = validator;
    }

    public async Task<TaskItem> HandleAsync(
        CreateTaskCommand command,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var project = await _projectRepository.GetByIdAsync(
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            throw new NotFoundException(
                $"Project with id '{command.ProjectId}' was not found.");
        }

        await _membershipService.EnsureMemberAsync(
            command.ProjectId,
            userId,
            cancellationToken);

        var now = DateTime.UtcNow;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = command.ProjectId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            Status = "Todo",
            Priority = command.Priority,
            AssignedUserId = command.AssignedUserId,
            DueDate = command.DueDate,
            IsBlocked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _taskRepository.AddAsync(task, cancellationToken);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        return task;
    }
}