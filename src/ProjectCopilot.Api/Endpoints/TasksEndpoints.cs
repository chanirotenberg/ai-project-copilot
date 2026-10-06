using System.Security.Claims;
using ProjectCopilot.Api.Authorization;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Tasks.CreateTask;
using ProjectCopilot.Application.Tasks.UpdateTask;

namespace ProjectCopilot.Api.Endpoints;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tasks");

        group.MapGet("/", async (
            Guid projectId,
            ITaskRepository repository,
            IProjectMembershipService membershipService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            await membershipService.EnsureMemberAsync(
                projectId,
                user.GetUserId(),
                cancellationToken);

            var tasks = await repository.GetByProjectIdAsync(
                projectId,
                cancellationToken);

            return Results.Ok(tasks);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (
            Guid id,
            ITaskRepository repository,
            IProjectMembershipService membershipService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var task = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (task is null)
            {
                throw new NotFoundException($"Task with id '{id}' was not found.");
            }

            await membershipService.EnsureMemberAsync(
                task.ProjectId,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(task);
        }).RequireAuthorization();

        group.MapPost("/", async (
            CreateTaskCommand command,
            CreateTaskHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var task = await handler.HandleAsync(command, user.GetUserId(), cancellationToken);

            return Results.Created(
                $"/api/v1/tasks/{task.Id}",
                task);
        }).RequireAuthorization();

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTaskRequest request,
            UpdateTaskHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateTaskCommand(
                id,
                request.Title,
                request.Description,
                request.Status,
                request.Priority,
                request.AssignedUserId,
                request.DueDate,
                request.IsBlocked);

            var task = await handler.HandleAsync(
                command,
                user.GetUserId(),
                cancellationToken);

            return Results.Ok(task);
        }).RequireAuthorization();

        return app;
    }
}

public sealed record UpdateTaskRequest(
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssignedUserId,
    DateTime? DueDate,
    bool IsBlocked);