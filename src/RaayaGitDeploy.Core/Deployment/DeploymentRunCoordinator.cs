using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentRunCoordinator(
    IGitRepositoryService gitRepository,
    DeploymentExecutor executor,
    IDeploymentHistoryStore historyStore)
{
    public async Task<DeploymentResult> ExecuteAsync(
        ServerProfile profile,
        DeploymentPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.IsDryRun)
            throw new InvalidOperationException("A dry-run deployment plan cannot mutate the remote server.");

        var context = plan.Context
            ?? throw new InvalidOperationException("Deployment plan is missing its reviewed Git context.");

        if (!string.Equals(profile.Id, context.ServerProfileId, StringComparison.Ordinal))
            throw new InvalidOperationException("Server profile changed after the deployment plan was reviewed.");

        var current = await gitRepository
            .GetContextAsync(context.RepositoryPath, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(current.BranchName, context.Branch, StringComparison.Ordinal))
            throw new InvalidOperationException("Repository branch changed after the deployment plan was reviewed.");

        if (!string.Equals(current.HeadSha, context.ToHead, StringComparison.Ordinal))
            throw new InvalidOperationException("Repository HEAD changed after the deployment plan was reviewed. Run Dry Run again before deploying.");

        var startedAt = DateTimeOffset.UtcNow;
        var result = await executor
            .ExecuteAsync(profile, plan, cancellationToken)
            .ConfigureAwait(false);
        var finishedAt = DateTimeOffset.UtcNow;

        var historyEntry = new DeploymentHistoryEntry(
            Guid.NewGuid().ToString("N"),
            startedAt,
            profile.Id,
            profile.DisplayName,
            result.Succeeded,
            result.Items,
            context.RepositoryPath,
            context.Branch,
            context.FromHead,
            context.ToHead,
            finishedAt);

        await historyStore.AppendAsync(historyEntry, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
