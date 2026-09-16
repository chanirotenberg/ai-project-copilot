using ProjectCopilot.Application.Abstractions;
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
            CancellationToken cancellationToken) =>
        {
            var tasks = await repository.GetByProjectIdAsync(
                projectId,
                cancellationToken);

            return Results.Ok(tasks);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ITaskRepository repository,
            CancellationToken cancellationToken) =>
        {
            var task = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return task is null
                ? Results.NotFound()
                : Results.Ok(task);
        });

        group.MapPost("/", async (
            CreateTaskCommand command,
            CreateTaskHandler handler,
            CancellationToken cancellationToken) =>
        {
            var task = await handler.HandleAsync(command, cancellationToken);

            return Results.Created(
                $"/api/v1/tasks/{task.Id}",
                task);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTaskRequest request,
            UpdateTaskHandler handler,
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
                cancellationToken);

            return Results.Ok(task);
        });

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