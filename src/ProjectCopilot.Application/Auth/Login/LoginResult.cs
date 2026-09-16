namespace ProjectCopilot.Application.Auth.Login;

public sealed record LoginResult(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Email);
