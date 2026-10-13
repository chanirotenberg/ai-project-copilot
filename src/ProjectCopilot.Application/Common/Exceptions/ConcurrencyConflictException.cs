namespace ProjectCopilot.Application.Common.Exceptions;

/// <summary>
/// Raised when a write lost an optimistic-concurrency race: the row changed between being read
/// and being saved.
/// </summary>
/// <remarks>
/// This is the Application-layer stand-in for EF Core's <c>DbUpdateConcurrencyException</c>,
/// which the Application layer must not reference. Repositories in Infrastructure translate at
/// the boundary — see <c>DocumentRepository.SaveChangesAsync</c>, and
/// <c>RefreshTokenRepository.SaveChangesAsync</c> for the same pattern.
///
/// It carries no HTTP meaning on its own: whether losing a race is a 409, a retry or something
/// else is a decision for the specific use case, so handlers that care translate it themselves
/// and anything that does not is reported as an unexpected failure.
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
