using FluentValidation;
using Microsoft.Extensions.Logging;
using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Common.Exceptions;

namespace ProjectCopilot.Application.Auth.Refresh;

public sealed class RefreshHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<RefreshCommand> _validator;
    private readonly ILogger<RefreshHandler> _logger;

    public RefreshHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenGenerator refreshTokenGenerator,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<RefreshCommand> validator,
        ILogger<RefreshHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenGenerator = refreshTokenGenerator;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _validator = validator;
        _logger = logger;
    }

    public async Task<LoginResult> HandleAsync(
        RefreshCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tokenHash = _refreshTokenGenerator.Hash(command.RefreshToken);

        var existing = await _refreshTokenRepository.FindByHashAsync(tokenHash, cancellationToken);

        if (existing is null)
        {
            throw new InvalidCredentialsException();
        }

        if (existing.RevokedAtUtc is not null)
        {
            // Reuse of an already-rotated (or already-revoked) refresh token: treat as a
            // possible token-theft signal and revoke the entire token family for this user.
            // This check must run BEFORE the expiry check below - a stolen token that is
            // replayed after its (fixed, never-shortened) expiry would otherwise hit the
            // expiry branch first and the reuse signal would be lost.
            // Never log the raw token or its hash - UserId only.
            await _refreshTokenRepository.RevokeAllActiveForUserAsync(existing.UserId, cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Refresh token reuse detected for UserId {UserId}; all active refresh tokens revoked.",
                existing.UserId);

            throw new InvalidCredentialsException();
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidCredentialsException();
        }

        var email = await _identityService.GetEmailByIdAsync(existing.UserId, cancellationToken);

        if (email is null)
        {
            throw new InvalidCredentialsException();
        }

        var newTokenId = Guid.NewGuid();
        var (rawRefreshToken, refreshTokenHash, refreshTokenExpiresAtUtc) = _refreshTokenGenerator.Generate();

        await _refreshTokenRepository.RevokeAsync(existing.Id, newTokenId, cancellationToken);

        await _refreshTokenRepository.AddAsync(
            newTokenId,
            existing.UserId,
            refreshTokenHash,
            refreshTokenExpiresAtUtc,
            cancellationToken);

        // IRefreshTokenRepository.SaveChangesAsync (Infrastructure) translates a concurrency
        // conflict on the old token's rotation (DbUpdateConcurrencyException) into
        // InvalidCredentialsException itself - Application does not and must not reference
        // EF Core, so it cannot catch that exception type here directly.
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAtUtc) = _jwtTokenGenerator.Generate(existing.UserId, email);

        return new LoginResult(accessToken, expiresAtUtc, existing.UserId, email, rawRefreshToken);
    }
}
