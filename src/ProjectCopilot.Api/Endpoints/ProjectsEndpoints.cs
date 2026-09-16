using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Projects.CreateProject;

namespace ProjectCopilot.Api.Endpoints;

public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects");

        group.MapGet("/", async (
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var projects = await repository.GetAllAsync(cancellationToken);

            return Results.Ok(projects);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProjectRepository repository,
            CancellationToken cancellationToken) =>
        {
            var project = await repository.GetByIdAsync(id, cancellationToken);

            return project is null
                ? Results.NotFound()
                : Results.Ok(project);
        });

        group.MapPost("/", async (
            CreateProjectCommand command,
            CreateProjectHandler handler,
            CancellationToken cancellationToken) =>
        {
            var project = await handler.HandleAsync(command, cancellationToken);

            return Results.Created(
                $"/api/v1/projects/{project.Id}",
                project);
        });

        return app;
    }
}