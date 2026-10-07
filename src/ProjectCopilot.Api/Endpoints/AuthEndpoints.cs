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
            // Anti-enumeration by design: a genuine new-account success and a duplicate-email
            // attempt both return this exact same 200 OK response with no distinguishing detail
            // (no userId/email echoed back) — see IdentityService.RegisterAsync. A thrown
            // ValidationException (genuine format/strength failure) still flows to the existing
            // 400 path via ExceptionHandlingMiddleware.
            await handler.HandleAsync(command, cancellationToken);

            return Results.Ok(new { message = "Registration request accepted." });
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
