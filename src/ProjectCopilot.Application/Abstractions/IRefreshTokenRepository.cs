namespace ProjectCopilot.Application.Abstractions;

// Primitive-based (not entity-typed) by design: the RefreshToken entity lives in
// ProjectCopilot.Infrastructure.Identity, and Application must not reference Infrastructure
// types in its own abstractions (same precedent as IJwtTokenGenerator, which only exposes
// Guid/string/DateTime - never ApplicationUser or JwtOptions - in its signature).
public interface IRefreshTokenRepository
{
    Task AddAsync(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRecord?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid tokenId,
        Guid? replacedByTokenId,
        CancellationToken cancellationToken = default);

    Task RevokeAllActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
