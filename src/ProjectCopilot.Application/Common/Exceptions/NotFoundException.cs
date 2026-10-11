namespace ProjectCopilot.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    /// <summary>
    /// Generic message for a 404 that must be indistinguishable from "the resource doesn't
    /// exist" — i.e. an authorization-driven 404 (caller lacks project membership). Never embed
    /// an id derived from the caller's own authorization context (e.g. a project id derived from
    /// a task/document the caller looked up by its own id) in this message; doing so would leak
    /// that id to a caller who isn't authorized to know it.
    /// </summary>
    public const string HiddenResourceMessage =
        "The requested resource was not found or you do not have access to it.";

    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a <see cref="NotFoundException"/> for an authorization-sensitive 404 — one where
    /// "doesn't exist" and "exists but you lack access" must render identically to the caller.
    /// </summary>
    public static NotFoundException HiddenResource() => new(HiddenResourceMessage);
}