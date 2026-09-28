using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentBaselineOnboardingService(
    IGitRepositoryService gitRepositoryService,
    IDeploymentHistoryStore historyStore)
{
    public async Task<DeploymentHistoryEntry> MarkCurrentHeadAsync(
        string repositoryPath,
        ServerProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(profile);

        var context = await gitRepositoryService
            .GetContextAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var entry = new DeploymentHistoryEntry(
            Guid.NewGuid().ToString("N"),
            now,
            profile.Id,
            profile.DisplayName,
            true,
            [],
            context.RepositoryPath,
            context.BranchName,
            null,
            context.HeadSha,
            now,
            DeploymentHistoryEventKind.BaselineMarked);

        await historyStore.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }
}
