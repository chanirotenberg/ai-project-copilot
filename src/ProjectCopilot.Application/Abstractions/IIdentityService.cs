namespace ProjectCopilot.Application.Abstractions;

public interface IIdentityService
{
    Task<Guid> RegisterAsync(
        string email,
        string password,
        CancellationToken ct);

    Task<(Guid UserId, string Email)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct);
}
