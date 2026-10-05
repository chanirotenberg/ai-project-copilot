using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;

namespace ProjectCopilot.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    // Fixed in-memory user used only to absorb the cost of a password-hash verification
    // when no matching account exists (see ValidateCredentialsAsync). Never persisted
    // to the database.
    private static readonly ApplicationUser DummyUser = new()
    {
        UserName = "dummy@timing-mitigation.local",
        Email = "dummy@timing-mitigation.local"
    };

    // Cached once for the process lifetime (NOT a per-instance field) since IdentityService
    // is registered AddScoped - a new instance per request. A per-instance Lazy<string> would
    // recompute the hash on every request, making the "user not found" branch below do TWO
    // expensive PBKDF2 operations (hash + verify) while "wrong password" only does ONE
    // (CheckPasswordAsync's internal verify), reintroducing (and worsening) the exact timing
    // gap this mitigation exists to close. Whichever request's IdentityService constructs
    // first seeds this static cache; safe, since PasswordHasher<TUser>'s behavior only depends
    // on the singleton-configured IOptions<PasswordHasherOptions>, not on which scope resolved
    // it - so it's still derived from the real configured hasher, just computed once.
    private static Lazy<string>? _dummyPasswordHash;
    private static readonly object DummyHashLock = new();

    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;

        if (_dummyPasswordHash is null)
        {
            lock (DummyHashLock)
            {
                _dummyPasswordHash ??= new Lazy<string>(() =>
                    userManager.PasswordHasher.HashPassword(
                        DummyUser, "Dummy-P@ssw0rd-For-Timing-Mitigation-Only"));
            }
        }
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
            _userManager.PasswordHasher.VerifyHashedPassword(DummyUser, _dummyPasswordHash!.Value, password);

            throw new InvalidCredentialsException();
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);

        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException();
        }

        return (user.Id, user.Email!);
    }

    public async Task<string?> GetEmailByIdAsync(
        Guid userId,
        CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user?.Email;
    }
}
