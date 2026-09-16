using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;

namespace ProjectCopilot.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    // Fixed in-memory user/hash used only to absorb the cost of a password-hash
    // verification when no matching account exists (see ValidateCredentialsAsync).
    // Never persisted to the database.
    private static readonly ApplicationUser DummyUser = new()
    {
        UserName = "dummy@timing-mitigation.local",
        Email = "dummy@timing-mitigation.local"
    };

    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(
            DummyUser,
            "Dummy-P@ssw0rd-For-Timing-Mitigation-Only");

    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Guid> RegisterAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            // Deliberately reusing FluentValidation.ValidationException here (not a layering
            // accident) so this failure flows through the existing 400 path in
            // ExceptionHandlingMiddleware instead of requiring a new exception type.
            // Any IdentityError.Code that isn't an Email/UserName duplicate is mapped to
            // "Password" as a deliberate simplification, since those are the only error
            // codes CreateAsync currently produces (e.g. weak-password rules).
            var failures = result.Errors
                .Select(error => new ValidationFailure(
                    error.Code.Contains("Email") || error.Code.Contains("UserName")
                        ? "Email"
                        : "Password",
                    error.Description))
                .ToList();

            throw new ValidationException(failures);
        }

        return user.Id;
    }

    public async Task<(Guid UserId, string Email)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            // Timing-attack mitigation: still perform a password-hash verification (against a
            // fixed dummy user/hash, result ignored) so this branch costs about the same as the
            // "user exists but wrong password" branch below. Without this, response time would
            // leak whether an email is registered.
            _userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, password);

            throw new InvalidCredentialsException();
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);

        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException();
        }

        return (user.Id, user.Email!);
    }
}
