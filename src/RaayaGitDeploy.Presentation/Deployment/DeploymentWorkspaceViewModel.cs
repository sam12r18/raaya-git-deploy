using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentWorkspaceViewModel
{
    private readonly DeploymentDryRunViewModel _dryRun;

    public DeploymentWorkspaceViewModel(
        DeploymentQueueViewModel queue,
        ServersViewModel servers,
        HostProfileEditorViewModel hostProfileEditor,
        RepositoryBranchSelectorViewModel repositoryBranchSelector,
        DeploymentProjectEditorViewModel projectEditor,
        DeploymentDryRunViewModel dryRun,
        DryRunSummaryViewModel dryRunSummary)
    {
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Servers = servers ?? throw new ArgumentNullException(nameof(servers));
        HostProfileEditor = hostProfileEditor ?? throw new ArgumentNullException(nameof(hostProfileEditor));
        RepositoryBranchSelector = repositoryBranchSelector ?? throw new ArgumentNullException(nameof(repositoryBranchSelector));
        ProjectEditor = projectEditor ?? throw new ArgumentNullException(nameof(projectEditor));
        _dryRun = dryRun ?? throw new ArgumentNullException(nameof(dryRun));
        DryRunSummary = dryRunSummary ?? throw new ArgumentNullException(nameof(dryRunSummary));
    }

    public DeploymentQueueViewModel Queue { get; }

    public ServersViewModel Servers { get; }

    public HostProfileEditorViewModel HostProfileEditor { get; }

    public RepositoryBranchSelectorViewModel RepositoryBranchSelector { get; }

    public DeploymentProjectEditorViewModel ProjectEditor { get; }

    public DryRunSummaryViewModel DryRunSummary { get; }

    public DeploymentPlan? PreviewPlan { get; private set; }

    public Task LoadServersAsync(CancellationToken cancellationToken) =>
        Servers.LoadAsync(cancellationToken);

    public async Task RefreshRepositoryAsync(CancellationToken cancellationToken)
    {
        await RepositoryBranchSelector.RefreshAsync(cancellationToken);
        SynchronizeProjectSource();
    }

    public void SynchronizeProjectSource()
    {
        ProjectEditor.RepositoryRoot = RepositoryBranchSelector.RepositoryPath.Trim();
        ProjectEditor.RemoteName = RepositoryBranchSelector.RemoteName.Trim();
        ProjectEditor.Branch = RepositoryBranchSelector.BranchName.Trim();

        if (Servers.SelectedProfile is not null)
        {
            ProjectEditor.ServerProfileId = Servers.SelectedProfile.Id;
        }
    }

    public bool TryCreateProject(out DeploymentProject? project)
    {
        SynchronizeProjectSource();

        if (RepositoryBranchSelector.State != RepositoryTrackingState.Success ||
            string.IsNullOrWhiteSpace(RepositoryBranchSelector.RemoteRevision))
        {
            project = null;
            return false;
        }

        return ProjectEditor.TryCreate(out project);
    }

    public DeploymentPlan CreateDryRunPreview(string repositoryRoot) =>
        _dryRun.Preview(repositoryRoot, Servers.SelectedProfile, Queue.Items);

    public void RefreshDryRunPreview(string repositoryRoot)
    {
        PreviewPlan = CreateDryRunPreview(repositoryRoot);
        DryRunSummary.Load(PreviewPlan);
    }
}
