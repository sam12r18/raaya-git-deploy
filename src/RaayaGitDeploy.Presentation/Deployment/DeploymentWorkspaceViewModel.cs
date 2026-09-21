using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentWorkspaceViewModel
{
    private readonly DeploymentDryRunViewModel _dryRun;
    private readonly DeploymentPlanner _planner;
    private readonly DeploymentExecutor _executor;
    private readonly IDeploymentHistoryStore _historyStore;
    private ServerProfile? _previewProfile;
    private int _executionInProgress;

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
    public bool IsExecuting => Volatile.Read(ref _executionInProgress) != 0;

    public Task LoadServersAsync(CancellationToken cancellationToken) => Servers.LoadAsync(cancellationToken);
    public async Task LoadHistoryAsync(CancellationToken cancellationToken) => History = await _historyStore.LoadAsync(cancellationToken);

    public DeploymentPlan CreateDryRunPreview(string repositoryRoot) =>
        _dryRun.Preview(repositoryRoot, Servers.SelectedProfile, Queue.Items);

    public void RefreshDryRunPreview(string repositoryRoot)
    {
        PreviewPlan = CreateDryRunPreview(repositoryRoot);
        _previewProfile = Servers.SelectedProfile;
    }

    public void PrepareRetry(DeploymentHistoryEntry entry, string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new InvalidOperationException("Open the repository associated with this deployment before preparing a retry.");
        if (entry.Succeeded)
            throw new InvalidOperationException("Successful deployments do not need recovery.");

        var profile = Servers.Profiles.FirstOrDefault(item => item.Id == entry.ServerProfileId)
            ?? throw new InvalidOperationException("The server profile used by this deployment is no longer available. Restore or select the intended profile before retrying.");

        var retryablePaths = entry.Items
            .Where(item => item.Status is DeploymentItemStatus.Failed or DeploymentItemStatus.Blocked)
            .Where(item => item.Operation.Kind == DeploymentOperationKind.Upload)
            .Select(item => item.Operation.LocalPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (retryablePaths.Length == 0)
            throw new InvalidOperationException("This failed deployment has no upload operations that can be safely re-queued automatically.");

        var normalizedRoot = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (retryablePaths.Any(path => !Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This deployment history contains files outside the currently open repository. Open the original repository before preparing a retry.");

        Queue.Clear();
        foreach (var localPath in retryablePaths)
            Queue.AddFile(localPath);

        Servers.SelectedProfile = profile;
        PreviewPlan = null;
        _previewProfile = null;
        LastResult = null;
    }

    public async Task<DeploymentResult> ExecuteAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _executionInProgress, 1, 0) != 0)
        {
            throw new InvalidOperationException("A deployment is already in progress.");
        }

        try
        {
            var profile = Servers.SelectedProfile ?? throw new InvalidOperationException("Select a server profile before deployment.");
            var preview = PreviewPlan ?? throw new InvalidOperationException("Run Dry Run before deployment.");

            var plan = _planner.Plan(
                repositoryRoot,
                profile.RemoteRoot,
                Queue.Items.Select(item => item.LocalPath),
                dryRun: false);

            if (_previewProfile is null || profile != _previewProfile || !preview.Operations.SequenceEqual(plan.Operations))
            {
                throw new InvalidOperationException("Deployment inputs changed after Dry Run. Run Dry Run again and review the updated plan before deploying.");
            }

            var startedAt = DateTimeOffset.UtcNow;
            var result = await _executor.ExecuteAsync(profile, plan, cancellationToken);
            LastResult = result;

            var entry = new DeploymentHistoryEntry(
                Guid.NewGuid().ToString("N"), startedAt, profile.Id, profile.DisplayName, result.Succeeded, result.Items);
            await _historyStore.AppendAsync(entry, cancellationToken);
            History = await _historyStore.LoadAsync(cancellationToken);
            return result;
        }
        finally
        {
            Interlocked.Exchange(ref _executionInProgress, 0);
        }
    }
}
