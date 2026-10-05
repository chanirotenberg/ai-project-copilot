namespace ProjectCopilot.Application.Abstractions;

public sealed record RefreshTokenRecord(
    Guid Id,
    Guid UserId,
    string TokenHash,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc);
