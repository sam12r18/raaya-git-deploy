namespace RaayaGitDeploy.Core.Deployment;

public sealed record DeploymentHistoryEntry(
    string Id,
    DateTimeOffset StartedAt,
    string ServerProfileId,
    string ServerDisplayName,
    bool Succeeded,
    IReadOnlyList<DeploymentItemResult> Items);

public interface IDeploymentHistoryStore
{
    Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken);
    Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken);
}
