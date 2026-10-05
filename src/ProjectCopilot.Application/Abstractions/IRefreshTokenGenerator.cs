namespace ProjectCopilot.Application.Abstractions;

// Split out from IJwtTokenGenerator (not merged into it) to keep that existing, unchanged
// interface untouched, and because refresh-token generation needs its own expiry policy
// (JwtOptions.RefreshTokenExpirationDays) - an Infrastructure-only configuration concern that
// Application must not reference directly.
public interface IRefreshTokenGenerator
{
    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) Generate();

    string Hash(string rawToken);
}
