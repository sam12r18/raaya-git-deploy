using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentBaselineOnboardingService(
    IGitRepositoryService gitRepositoryService,
    IDeploymentHistoryStore historyStore)
{
    private const int BaselineCommitLookupLimit = 200;

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

        return await AppendBaselineAsync(context, profile, context.HeadSha, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeploymentHistoryEntry> MarkRecentCommitAsync(
        string repositoryPath,
        ServerProfile profile,
        string commitSha,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        var context = await gitRepositoryService
            .GetContextAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(context.HeadSha, commitSha, StringComparison.OrdinalIgnoreCase))
            return await AppendBaselineAsync(context, profile, context.HeadSha, cancellationToken).ConfigureAwait(false);

        var recentCommits = await gitRepositoryService
            .GetRecentCommitsAsync(context.RootPath, BaselineCommitLookupLimit, cancellationToken)
            .ConfigureAwait(false);

        var selectedCommit = recentCommits.FirstOrDefault(commit =>
            string.Equals(commit.Sha, commitSha, StringComparison.OrdinalIgnoreCase));

        if (selectedCommit is null)
            throw new InvalidOperationException("The selected baseline must be a recent commit reachable from the current HEAD.");

        return await AppendBaselineAsync(context, profile, selectedCommit.Sha, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DeploymentHistoryEntry> AppendBaselineAsync(
        GitRepositoryContext context,
        ServerProfile profile,
        string baselineSha,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new DeploymentHistoryEntry(
            Guid.NewGuid().ToString("N"),
            now,
            profile.Id,
            profile.DisplayName,
            true,
            [],
            context.RootPath,
            context.BranchName,
            null,
            baselineSha,
            now,
            DeploymentHistoryEventKind.BaselineMarked);

        await historyStore.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }
}
