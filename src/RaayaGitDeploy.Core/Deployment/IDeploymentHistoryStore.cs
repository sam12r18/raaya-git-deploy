namespace RaayaGitDeploy.Core.Deployment;

public sealed record DeploymentHistoryEntry(
    string Id,
    DateTimeOffset StartedAt,
    string ServerProfileId,
    string ServerDisplayName,
    bool Succeeded,
    IReadOnlyList<DeploymentItemResult> Items,
    string? RepositoryPath = null,
    string? Branch = null,
    string? FromHead = null,
    string? ToHead = null,
    DateTimeOffset? FinishedAt = null);

public interface IDeploymentHistoryStore
{
    Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken);
    Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken);
}
