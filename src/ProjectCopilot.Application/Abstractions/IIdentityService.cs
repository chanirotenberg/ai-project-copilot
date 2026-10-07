namespace ProjectCopilot.Application.Abstractions;

public interface IIdentityService
{
    Task RegisterAsync(
        string email,
        string password,
        CancellationToken ct);

    Task<(Guid UserId, string Email)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct);

    Task<string?> GetEmailByIdAsync(
        Guid userId,
        CancellationToken ct);
}
