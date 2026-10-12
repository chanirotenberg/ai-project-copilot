namespace ProjectCopilot.Application.Abstractions;

public interface IProjectMembershipService
{
    /// <summary>
    /// Verifies that <paramref name="userId"/> is a member of <paramref name="projectId"/>.
    /// Throws <see cref="ProjectCopilot.Application.Common.Exceptions.NotFoundException"/> when
    /// not — identical in shape to the project not existing at all, so callers (and clients)
    /// cannot distinguish "doesn't exist" from "exists but you have no access".
    /// </summary>
    Task EnsureMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether <paramref name="userId"/> is a member of <paramref name="projectId"/>,
    /// without throwing. Intended for callers that need a boolean (e.g. validating that a
    /// different user id, such as a task's AssignedUserId, is a project member) where a 404
    /// "hidden resource" response would be the wrong status code for the failure case.
    /// </summary>
    Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
