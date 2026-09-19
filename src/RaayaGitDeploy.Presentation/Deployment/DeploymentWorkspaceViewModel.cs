using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentWorkspaceViewModel
{
    private readonly DeploymentDryRunViewModel _dryRun;

    public DeploymentWorkspaceViewModel(
        DeploymentQueueViewModel queue,
        ServersViewModel servers,
        DeploymentDryRunViewModel dryRun)
    {
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _dryRun = dryRun ?? throw new ArgumentNullException(nameof(dryRun));
    }

    public DeploymentQueueViewModel Queue { get; }

    public ServersViewModel Servers { get; }

    public Task LoadServersAsync(CancellationToken cancellationToken) =>
        Servers.LoadAsync(cancellationToken);

    public DeploymentPlan CreateDryRunPreview(string repositoryRoot) =>
        _dryRun.Preview(repositoryRoot, Servers.SelectedProfile, Queue.Items);
}
