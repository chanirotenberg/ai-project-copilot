using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Auth.Refresh;
using ProjectCopilot.Application.Auth.Register;

namespace ProjectCopilot.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/register", async (
            RegisterCommand command,
            RegisterHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            // Location is symbolic — no GET /api/v1/auth/users/{id} endpoint exists yet to
            // resolve it. Still returning 201/Created since a resource was in fact created.
            return Results.Created(
                $"/api/v1/auth/users/{result.UserId}",
                result);
        });

        group.MapPost("/login", async (
            LoginCommand command,
            LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            return Results.Ok(result);
        });

        // Anonymous by design, same as /login - the refresh token itself is the credential.
        group.MapPost("/refresh", async (
            RefreshCommand command,
            RefreshHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            return Results.Ok(result);
        });

        return app;
    }
}
