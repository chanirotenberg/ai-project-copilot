using FluentValidation;
using ProjectCopilot.Application.Abstractions;

namespace ProjectCopilot.Application.Auth.Login;

public sealed class LoginHandler
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IValidator<LoginCommand> _validator;

    public LoginHandler(
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        IValidator<LoginCommand> validator)
    {
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _validator = validator;
    }

    public async Task<LoginResult> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var (userId, email) = await _identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            cancellationToken);

        var (accessToken, expiresAtUtc) = _jwtTokenGenerator.Generate(userId, email);

        var (rawRefreshToken, refreshTokenHash, refreshTokenExpiresAtUtc) = _refreshTokenGenerator.Generate();

        await _refreshTokenRepository.AddAsync(
            Guid.NewGuid(),
            userId,
            refreshTokenHash,
            refreshTokenExpiresAtUtc,
            cancellationToken);

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new LoginResult(accessToken, expiresAtUtc, userId, email, rawRefreshToken);
    }
}
