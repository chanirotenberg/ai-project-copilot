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
}
