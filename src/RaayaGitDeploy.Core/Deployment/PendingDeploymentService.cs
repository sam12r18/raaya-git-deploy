using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed class PendingDeploymentService(
    IGitRepositoryService gitRepository,
    DeploymentBaselineService baselineService)
{
    public async Task<PendingDeploymentSnapshot> BuildAsync(
        string repositoryPath,
        string serverProfileId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverProfileId);

        var context = await gitRepository
            .GetContextAsync(repositoryPath, cancellationToken)
            .ConfigureAwait(false);

        var baseline = await baselineService
            .GetLastSuccessfulAsync(context.RootPath, serverProfileId, cancellationToken)
            .ConfigureAwait(false);

        if (baseline is null)
        {
            return new PendingDeploymentSnapshot(
                context.RootPath,
                context.BranchName,
                FromHead: null,
                context.HeadSha,
                Commits: [],
                Changes: [],
                RequiresBaseline: true);
        }

        var fromHead = baseline.ToHead!;
        if (string.Equals(fromHead, context.HeadSha, StringComparison.Ordinal))
        {
            return new PendingDeploymentSnapshot(
                context.RootPath,
                context.BranchName,
                fromHead,
                context.HeadSha,
                Commits: [],
                Changes: [],
                RequiresBaseline: false);
        }

        var commits = await gitRepository
            .GetCommitsBetweenAsync(context.RootPath, fromHead, context.HeadSha, cancellationToken)
            .ConfigureAwait(false);
        var changes = await gitRepository
            .GetChangesBetweenAsync(context.RootPath, fromHead, context.HeadSha, cancellationToken)
            .ConfigureAwait(false);

        return new PendingDeploymentSnapshot(
            context.RootPath,
            context.BranchName,
            fromHead,
            context.HeadSha,
            commits,
            changes,
            RequiresBaseline: false);
    }
}
