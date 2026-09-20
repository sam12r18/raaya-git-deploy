using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentWorkspaceViewModel
{
    private readonly DeploymentDryRunViewModel _dryRun;

    public DeploymentWorkspaceViewModel(
        DeploymentQueueViewModel queue,
        ServersViewModel servers,
        DeploymentDryRunViewModel dryRun,
        DryRunSummaryViewModel dryRunSummary)
    {
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _dryRun = dryRun ?? throw new ArgumentNullException(nameof(dryRun));
        DryRunSummary = dryRunSummary ?? throw new ArgumentNullException(nameof(dryRunSummary));
    }

    public DeploymentQueueViewModel Queue { get; }

    public ServersViewModel Servers { get; }

    public DryRunSummaryViewModel DryRunSummary { get; }

    public DeploymentPlan? PreviewPlan { get; private set; }

    public Task LoadServersAsync(CancellationToken cancellationToken) =>
        Servers.LoadAsync(cancellationToken);

    public DeploymentPlan CreateDryRunPreview(string repositoryRoot) =>
        _dryRun.Preview(repositoryRoot, Servers.SelectedProfile, Queue.Items);

    public void RefreshDryRunPreview(string repositoryRoot)
    {
        PreviewPlan = CreateDryRunPreview(repositoryRoot);
        DryRunSummary.Load(PreviewPlan);
    }
}
