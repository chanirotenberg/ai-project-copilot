using System.Security.Claims;
using ProjectCopilot.Api.Authorization;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Application.Projects.CreateProject;

namespace ProjectCopilot.Api.Endpoints;

public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects");

        group.MapGet("/", async (
            IProjectRepository repository,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var projects = await repository.GetAllForUserAsync(user.GetUserId(), cancellationToken);

            return Results.Ok(projects);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProjectRepository repository,
            IProjectMembershipService membershipService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var project = await repository.GetByIdAsync(id, cancellationToken);

            if (project is null)
            {
                // Must stay textually identical to the membership-check 404 below — same
                // indistinguishability rationale as Tasks/Documents GET-by-id endpoints.
                throw NotFoundException.HiddenResource();
            }

            await membershipService.EnsureMemberAsync(id, user.GetUserId(), cancellationToken);

            return Results.Ok(project);
        }).RequireAuthorization();

        group.MapPost("/", async (
            CreateProjectCommand command,
            CreateProjectHandler handler,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var project = await handler.HandleAsync(command, user.GetUserId(), cancellationToken);

            return Results.Created(
                $"/api/v1/projects/{project.Id}",
                project);
        }).RequireAuthorization();

        return app;
    }
}