using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentDryRunViewModel(DeploymentPlanner planner)
{
    private readonly DeploymentPlanner _planner = planner ?? throw new ArgumentNullException(nameof(planner));

    public DeploymentPlan Preview(
        string repositoryRoot,
        ServerProfile? selectedProfile,
        IEnumerable<DeploymentQueueItem> queueItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(queueItems);

        if (selectedProfile is null)
        {
            throw new InvalidOperationException("Select a server before creating a deployment preview.");
        }

        return _planner.Plan(
            repositoryRoot,
            selectedProfile.RemoteRoot,
            queueItems.Select(item => item.LocalPath),
            dryRun: true);
    }
}
