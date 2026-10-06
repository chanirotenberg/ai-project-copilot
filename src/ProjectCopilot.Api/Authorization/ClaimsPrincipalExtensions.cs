using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ProjectCopilot.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated user's id from the "sub" claim issued by <c>JwtTokenGenerator</c>,
    /// falling back to <see cref="ClaimTypes.NameIdentifier"/> defensively. Throws rather than
    /// returning <see cref="Guid.Empty"/> or any other silent default when the claim is missing
    /// or unparseable — authorization code must never proceed with an unverified identity.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (value is null || !Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException(
                "Authenticated request is missing a valid user id claim.");
        }

        return userId;
    }
}
