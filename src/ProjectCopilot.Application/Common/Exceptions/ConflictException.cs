namespace ProjectCopilot.Application.Common.Exceptions;

/// <summary>
/// Raised when a request is well-formed and authorized but conflicts with the current state of
/// the resource (HTTP 409). Only ever thrown after the caller's authorization has been
/// established, so its message may describe the resource state.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
