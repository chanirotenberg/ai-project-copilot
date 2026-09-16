using ProjectCopilot.Application.Auth.Login;
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

        return app;
    }
}
