using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Common.Exceptions;
using ProjectCopilot.Infrastructure.Identity;
using ProjectCopilot.Infrastructure.Persistence;

namespace ProjectCopilot.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var token = new RefreshToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _dbContext.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public async Task<RefreshTokenRecord?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        return token is null
            ? null
            : new RefreshTokenRecord(token.Id, token.UserId, token.TokenHash, token.ExpiresAtUtc, token.RevokedAtUtc);
    }

    public async Task RevokeAsync(
        Guid tokenId,
        Guid? replacedByTokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _dbContext.RefreshTokens
            .FindAsync(new object?[] { tokenId }, cancellationToken);

        if (token is null)
        {
            return;
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        token.ReplacedByTokenId = replacedByTokenId;
    }

    public async Task RevokeAllActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent rotation already moved this token forward (its RowVersion/xmin no
            // longer matches what we read). Treat it the same as "token no longer valid" rather
            // than surfacing a 500 - Application must not reference EF Core, so this translation
            // happens here, at the only layer that is allowed to know about DbUpdateConcurrencyException.
            throw new InvalidCredentialsException();
        }
    }
}
