namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentBaselineService(IDeploymentHistoryStore store)
{
    public async Task<DeploymentHistoryEntry?> GetLastSuccessfulAsync(
        string repositoryPath,
        string serverProfileId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverProfileId);

        var targetRepository = NormalizeRepositoryPath(repositoryPath);
        var entries = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

        return entries
            .Where(entry =>
                entry.Succeeded &&
                !string.IsNullOrWhiteSpace(entry.RepositoryPath) &&
                !string.IsNullOrWhiteSpace(entry.ToHead) &&
                string.Equals(
                    NormalizeRepositoryPath(entry.RepositoryPath!),
                    targetRepository,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.ServerProfileId, serverProfileId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.FinishedAt ?? entry.StartedAt)
            .FirstOrDefault();
    }

    private static string NormalizeRepositoryPath(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
