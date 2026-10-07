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

    // KNOWN GAP (tracked, not fixed here): a true race between two concurrent registration
    // requests for the same email can collide at the DB-level unique constraint on AspNetUsers
    // after both pass Identity's in-memory uniqueness pre-check. The losing request's
    // DbUpdateException is currently unhandled here and surfaces as a 500, not the intended
    // generic 200 - a narrow exception to the anti-enumeration guarantee in that race window.
    // Pre-existing pattern, not introduced by this pass.
    public async Task RegisterAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded) return; // real account created

        var isDuplicateOnly = result.Errors.All(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
        if (isDuplicateOnly)
        {
            // Anti-enumeration: no additional dummy password hash is added here. UserManager
            // .CreateAsync always hashes the password (inside UpdatePasswordHash) BEFORE running
            // uniqueness validation, regardless of outcome - confirmed by reading its actual call
            // order. So this duplicate-email path already pays the same hashing cost as a real
            // success; adding a second hash here would double it on THIS path specifically and
            // reverse the timing signal this comment exists to avoid. This is a genuine silent
            // no-op: CreateAsync already failed internally, no user row was inserted, nothing
            // further to do.
            return;
        }

        // Genuine format/strength failures - not an enumeration signal, reported normally,
        // identical whether the submitted email is new or already registered.
        throw new ValidationException(result.Errors.Select(e =>
            new ValidationFailure("Password", e.Description)));
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
