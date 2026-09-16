namespace ProjectCopilot.Application.Abstractions;

public interface IJwtTokenGenerator
{
    (string AccessToken, DateTime ExpiresAtUtc) Generate(Guid userId, string email);
}
