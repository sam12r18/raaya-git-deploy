namespace RaayaGitDeploy.Core.Companion;

public sealed record CompanionRepository(string Id, string DisplayName, string Branch, string HeadSha, bool HasChanges);

public sealed record CompanionDeploymentProfile(string Id, string DisplayName, string Environment, bool RequiresConfirmation);

public sealed record CompanionDeploymentRequest(string RepositoryId, string ProfileId, IReadOnlyList<string> Paths, bool DryRun);

public sealed record CompanionDeploymentOperation(string Path, string RemotePath, string Kind);

public sealed record CompanionDeploymentPreview(string Id, string RepositoryId, string ProfileId, IReadOnlyList<CompanionDeploymentOperation> Operations, DateTimeOffset CreatedAt);

public sealed record CompanionDeploymentRun(string Id, string RepositoryId, string ProfileId, string State, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? FailureMessage);

public interface ICompanionDeploymentApi
{
    Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken);
    Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken);
    Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken);
    Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken);
}
