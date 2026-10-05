using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectCopilot.Application.Abstractions;

namespace ProjectCopilot.Infrastructure.Identity;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private readonly JwtOptions _options;

    public RefreshTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string RawToken, string TokenHash, DateTime ExpiresAtUtc) Generate()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

        return (rawToken, Hash(rawToken), expiresAtUtc);
    }

    public string Hash(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
