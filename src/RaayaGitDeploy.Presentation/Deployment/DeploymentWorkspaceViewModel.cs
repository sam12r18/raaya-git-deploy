using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentWorkspaceViewModel
{
    private readonly DeploymentDryRunViewModel _dryRun;
    private readonly DeploymentPlanner _planner;
    private readonly DeploymentExecutor _executor;
    private readonly IDeploymentHistoryStore _historyStore;

    public DeploymentWorkspaceViewModel(
        DeploymentQueueViewModel queue,
        ServersViewModel servers,
        DeploymentDryRunViewModel dryRun,
        DeploymentPlanner planner,
        DeploymentExecutor executor,
        IDeploymentHistoryStore historyStore)
    {
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _dryRun = dryRun ?? throw new ArgumentNullException(nameof(dryRun));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
    }

    public DeploymentQueueViewModel Queue { get; }
    public ServersViewModel Servers { get; }
    public DeploymentPlan? PreviewPlan { get; private set; }
    public DeploymentResult? LastResult { get; private set; }
    public IReadOnlyList<DeploymentHistoryEntry> History { get; private set; } = Array.Empty<DeploymentHistoryEntry>();

    public Task LoadServersAsync(CancellationToken cancellationToken) => Servers.LoadAsync(cancellationToken);
    public async Task LoadHistoryAsync(CancellationToken cancellationToken) => History = await _historyStore.LoadAsync(cancellationToken);

    public DeploymentPlan CreateDryRunPreview(string repositoryRoot) =>
        _dryRun.Preview(repositoryRoot, Servers.SelectedProfile, Queue.Items);

    public void RefreshDryRunPreview(string repositoryRoot) => PreviewPlan = CreateDryRunPreview(repositoryRoot);

    public async Task<DeploymentResult> ExecuteAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var profile = Servers.SelectedProfile ?? throw new InvalidOperationException("Select a server profile before deployment.");
        if (PreviewPlan is null) throw new InvalidOperationException("Run Dry Run before deployment.");

        var plan = _planner.Plan(repositoryRoot, profile.RemoteRoot, Queue.Items, dryRun: false);
        var startedAt = DateTimeOffset.UtcNow;
        var result = await _executor.ExecuteAsync(profile, plan, cancellationToken);
        LastResult = result;

        var entry = new DeploymentHistoryEntry(
            Guid.NewGuid().ToString("N"), startedAt, profile.Id, profile.DisplayName, result.Succeeded, result.Items);
        await _historyStore.AppendAsync(entry, cancellationToken);
        History = await _historyStore.LoadAsync(cancellationToken);
        return result;
    }
}
